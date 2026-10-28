// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

[McpServerToolType]
public partial class Tools(ISqlConnectionFactory connectionFactory, IDatabaseRegistry databaseRegistry, ILogger<Tools> logger)
{
    private const string DatabaseKeyDescription = "Key of the database to use. Call list_databases to get the configured keys.";

    private readonly ISqlConnectionFactory _connectionFactory = connectionFactory;

    private readonly IDatabaseRegistry _databaseRegistry = databaseRegistry;

    private readonly ILogger<Tools> _logger = logger;

    /// <summary>
    /// Opens a connection to a database, runs an operation on it, and records the database in the result.
    /// </summary>
    /// <param name="databaseKey">Key of the database to connect to.</param>
    /// <param name="readOnly">Use the read-only connection string?</param>
    /// <param name="operationName">Name of the operation, for logging.</param>
    /// <param name="operation">Operation to run on the open connection.</param>
    /// <returns>Result of the operation, or an error result if connecting or the operation failed.</returns>
    private async Task<DbOperationResult> ExecuteAsync(string databaseKey, bool readOnly, string operationName, Func<SqlConnection, Task<DbOperationResult>> operation)
    {
        if (string.IsNullOrWhiteSpace(databaseKey))
        {
            return new DbOperationResult(success: false, error: "Database key parameter cannot be null/empty/whitespace");
        }

        SqlConnection connection;

        try
        {
            connection = readOnly
                ? await _connectionFactory.GetOpenReadOnlyConnectionAsync(databaseKey)
                : await _connectionFactory.GetOpenConnectionAsync(databaseKey);
        }
        catch (DatabaseInactiveException ex)
        {
            _logger.LogWarning("Tool called for inactive database {DatabaseKey}", databaseKey);

            return new DbOperationResult(success: false, error: ex.Message) { DatabaseKey = databaseKey };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database {DatabaseKey}: {Message}", databaseKey, ex.Message);

            return new DbOperationResult(success: false, error: $"Failed to connect to database: {ex.Message}") { DatabaseKey = databaseKey };
        }

        await using (connection)
        {
            string databaseName = connection.Database;

            DbOperationResult result;

            try
            {
                result = await operation(connection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{OperationName} failed: {Message}", operationName, ex.Message);

                result = new DbOperationResult(success: false, error: ex.Message);
            }

            result.DatabaseKey = databaseKey;
            result.DatabaseName = databaseName;

            return result;
        }
    }
}
