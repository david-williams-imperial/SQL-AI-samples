// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using Microsoft.Data.SqlClient;

namespace Mssql.McpServer;

/// <summary>
/// Command-line commands for managing the databases in the configuration database.
/// </summary>
public static class DatabaseCommands
{
    private const string SetDatabaseCommand = "set-database";

    private const string RemoveDatabaseCommand = "remove-database";

    private const string ListDatabasesCommand = "list-databases";

    private static readonly string[] HelpCommands = ["help", "--help", "-h", "/?"];

    /// <summary>
    /// Determines whether the command-line arguments are a management command, rather than a request to run the MCP server.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>True if the arguments are a management command.</returns>
    public static bool IsCommand(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        string command = args[0].ToLowerInvariant();

        return command is SetDatabaseCommand or RemoveDatabaseCommand or ListDatabasesCommand || HelpCommands.Contains(command);
    }

    /// <summary>
    /// Runs a management command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Process exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(SqliteDatabaseRegistry.GetConfiguredPath());

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case SetDatabaseCommand when args.Length == 2:
                    return await SetDatabaseAsync(registry, args[1]);
                case RemoveDatabaseCommand when args.Length == 2:
                    return await RemoveDatabaseAsync(registry, args[1]);
                case ListDatabasesCommand when args.Length == 1:
                    return await ListDatabasesAsync(registry);
                default:
                    WriteUsage(registry);

                    return HelpCommands.Contains(args[0].ToLowerInvariant()) ? 0 : 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");

            return 1;
        }
    }

    /// <summary>
    /// Prompts for connection strings, then adds or updates a database.
    /// </summary>
    /// <param name="registry">Configuration database.</param>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>Process exit code.</returns>
    private static async Task<int> SetDatabaseAsync(SqliteDatabaseRegistry registry, string databaseKey)
    {
        databaseKey = databaseKey.Trim();

        if (string.IsNullOrWhiteSpace(databaseKey))
        {
            Console.Error.WriteLine("Error: A database key is required.");

            return 1;
        }

        string readOnlyConnectionString = ReadSecret("Read-only connection string: ");

        if (string.IsNullOrWhiteSpace(readOnlyConnectionString))
        {
            Console.Error.WriteLine("Error: A read-only connection string is required.");

            return 1;
        }

        string writeConnectionString = ReadSecret("Write connection string (blank for read-only access): ");

        Console.Write("Environment, e.g. Dev or Live (blank to leave unchanged): ");

        string environmentName = (Console.ReadLine() ?? string.Empty).Trim();

        Console.Write("Description, e.g. Main 3sixty database (blank to leave unchanged): ");

        string description = (Console.ReadLine() ?? string.Empty).Trim();

        // Parse both before saving, so typos are reported now rather than on first use
        _ = new SqlConnectionStringBuilder(readOnlyConnectionString);

        if (!string.IsNullOrWhiteSpace(writeConnectionString))
        {
            _ = new SqlConnectionStringBuilder(writeConnectionString);
        }

        await registry.SetDatabaseAsync(databaseKey, readOnlyConnectionString, writeConnectionString, environmentName, description);

        Console.WriteLine($"Saved '{databaseKey}' to {registry.DatabasePath}");

        return 0;
    }

    /// <summary>
    /// Removes a database.
    /// </summary>
    /// <param name="registry">Configuration database.</param>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>Process exit code.</returns>
    private static async Task<int> RemoveDatabaseAsync(SqliteDatabaseRegistry registry, string databaseKey)
    {
        bool removed = await registry.RemoveDatabaseAsync(databaseKey);

        if (!removed)
        {
            Console.Error.WriteLine($"Error: Database key '{databaseKey}' was not found in {registry.DatabasePath}");

            return 1;
        }

        Console.WriteLine($"Removed '{databaseKey}' from {registry.DatabasePath}");

        return 0;
    }

    /// <summary>
    /// Lists the configured databases, showing the server and database of each connection string.
    /// </summary>
    /// <param name="registry">Configuration database.</param>
    /// <returns>Process exit code.</returns>
    private static async Task<int> ListDatabasesAsync(SqliteDatabaseRegistry registry)
    {
        Console.WriteLine($"Configuration database: {registry.DatabasePath}");

        IReadOnlyList<DatabaseSummary> summaries = await registry.GetDatabaseSummariesAsync(activeOnly: false);

        if (summaries.Count == 0)
        {
            Console.WriteLine("No databases are configured.");

            return 0;
        }

        foreach (DatabaseSummary summary in summaries)
        {
            Console.WriteLine();
            Console.WriteLine($"{summary.DatabaseKey} [{summary.EnvironmentName}]{(summary.IsActive ? string.Empty : " (inactive)")}");

            if (!string.IsNullOrWhiteSpace(summary.Description))
            {
                Console.WriteLine($"  {summary.Description}");
            }

            try
            {
                DatabaseRegistration? registration = await registry.GetDatabaseAsync(summary.DatabaseKey);

                Console.WriteLine($"  Read-only: {DescribeTarget(registration?.ReadOnlyConnectionString)}");
                Console.WriteLine($"  Write:     {DescribeTarget(registration?.WriteConnectionString)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Describes the server and database of a connection string, without credentials.
    /// </summary>
    /// <param name="connectionString">Connection string, or null.</param>
    /// <returns>Description.</returns>
    private static string DescribeTarget(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(none)";
        }

        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

        string database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "(default database)" : builder.InitialCatalog;

        return $"{builder.DataSource} / {database}";
    }

    /// <summary>
    /// Reads a line from the console without echoing it, as connection strings may contain passwords.
    /// Input is read normally if redirected.
    /// </summary>
    /// <param name="prompt">Prompt to display.</param>
    /// <returns>Text entered, trimmed.</returns>
    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);

        if (Console.IsInputRedirected)
        {
            string line = Console.ReadLine() ?? string.Empty;

            Console.WriteLine();

            return line.Trim();
        }

        StringBuilder input = new StringBuilder();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (input.Length > 0)
                {
                    input.Length--;
                    Console.Write("\b \b");
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                input.Append(key.KeyChar);
                Console.Write('*');
            }
        }

        Console.WriteLine();

        return input.ToString().Trim();
    }

    /// <summary>
    /// Writes command-line usage.
    /// </summary>
    /// <param name="registry">Configuration database.</param>
    private static void WriteUsage(SqliteDatabaseRegistry registry)
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  MssqlMcp                        Run the MCP server (stdio)");
        Console.WriteLine($"  MssqlMcp {SetDatabaseCommand} <key>     Add or update a database (prompts for connection strings)");
        Console.WriteLine($"  MssqlMcp {RemoveDatabaseCommand} <key>  Remove a database");
        Console.WriteLine($"  MssqlMcp {ListDatabasesCommand}          List databases");
        Console.WriteLine();
        Console.WriteLine($"Configuration database: {registry.DatabasePath}");
        Console.WriteLine($"Set the {SqliteDatabaseRegistry.PathEnvironmentVariable} environment variable to use a different location.");
        Console.WriteLine("Connection strings are encrypted for the current Windows user.");
    }
}
