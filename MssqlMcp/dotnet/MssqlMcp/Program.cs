// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mssql.McpServer;

internal class Program
{
    private static async Task Main(string[] args)
    {
        if (DatabaseCommands.IsCommand(args))
        {
            Environment.ExitCode = await DatabaseCommands.RunAsync(args);

            return;
        }

        SqliteDatabaseRegistry databaseRegistry = new SqliteDatabaseRegistry(SqliteDatabaseRegistry.GetConfiguredPath());

        await UpgradeConfigurationDatabaseAsync(databaseRegistry);

        string serverInstructions = await GetServerInstructionsAsync(databaseRegistry);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        builder.Services.AddSingleton<IDatabaseRegistry>(databaseRegistry);
        builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        builder.Services.AddSingleton<Tools>();

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInstructions = serverInstructions;
            })
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        IHost host = builder.Build();

        using (CancellationTokenSource cts = new CancellationTokenSource())
        {
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await host.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                if (host.Services.GetService(typeof(ILogger<Program>)) is ILogger<Program> logger)
                {
                    logger.LogCritical(ex, "Unhandled exception occurred during host execution.");
                }
                else
                {
                    Console.Error.WriteLine($"Unhandled exception: {ex}");
                }

                Environment.ExitCode = 1;
            }
        }
    }

    /// <summary>
    /// Applies any outstanding schema revisions to the configuration database, reporting them on standard error.
    /// Failures are reported but do not stop the server starting; tools report the error when called.
    /// </summary>
    /// <param name="databaseRegistry">Configuration database.</param>
    /// <returns>A task that completes when the configuration database has been checked.</returns>
    private static async Task UpgradeConfigurationDatabaseAsync(SqliteDatabaseRegistry databaseRegistry)
    {
        if (!databaseRegistry.ConfigurationDatabaseExists)
        {
            return;
        }

        try
        {
            IReadOnlyList<int> appliedRevisionNumbers = await databaseRegistry.UpgradeAsync();

            if (appliedRevisionNumbers.Count > 0)
            {
                Console.Error.WriteLine($"Upgraded configuration database '{databaseRegistry.DatabasePath}' to schema revision {appliedRevisionNumbers[^1]}.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to upgrade configuration database: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the instructions sent to MCP clients on initialisation, listing the configured database keys and their environments.
    /// Keys added after start-up are still usable; they are just not listed here until the server restarts.
    /// </summary>
    /// <param name="databaseRegistry">Source of the configured databases.</param>
    /// <returns>Server instructions.</returns>
    private static async Task<string> GetServerInstructionsAsync(IDatabaseRegistry databaseRegistry)
    {
        const string Instructions =
            "Connects to multiple SQL Server databases. " +
            "Every tool except list_databases requires a databaseKey argument that selects the database to use. " +
            "Call list_databases for the server and database name behind each key. " +
            "Each database is tagged with its environment (e.g. Dev or Live); take particular care with Live databases.";

        try
        {
            IReadOnlyList<DatabaseSummary> summaries = await databaseRegistry.GetDatabaseSummariesAsync(activeOnly: true);

            if (summaries.Count == 0)
            {
                return $"{Instructions} No databases are configured yet.";
            }

            IEnumerable<string> keys = summaries.Select(s => $"{s.DatabaseKey} ({s.EnvironmentName})");

            return $"{Instructions} Configured database keys: {string.Join(", ", keys)}.";
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unable to read database keys: {ex.Message}");

            return Instructions;
        }
    }
}
