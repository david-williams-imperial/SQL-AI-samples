// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    /// <summary>
    /// Lists the active databases, without connecting to them. Passwords are never included.
    /// </summary>
    /// <returns>Result containing one entry per database key.</returns>
    [McpServerTool(
        Title = "List Databases",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists the databases this MCP server can connect to, with a description of each and the environment it belongs to (e.g. Dev or Live). " +
                    "Every other tool requires one of the returned database keys. " +
                    "Databases without a write connection are read-only.")]
    public async Task<DbOperationResult> ListDatabases()
    {
        IReadOnlyList<DatabaseSummary> summaries;

        try
        {
            summaries = await _databaseRegistry.GetDatabaseSummariesAsync(activeOnly: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListDatabases failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }

        List<Dictionary<string, object?>> databases = new List<Dictionary<string, object?>>();

        foreach (DatabaseSummary summary in summaries)
        {
            string key = summary.DatabaseKey;

            Dictionary<string, object?> database = new Dictionary<string, object?>
            {
                ["database_key"] = key,
                ["environment"] = summary.EnvironmentName,
                ["description"] = summary.Description,
                ["error"] = null
            };

            try
            {
                DatabaseRegistration? registration = await _databaseRegistry.GetDatabaseAsync(key);

                // The database may have been removed or made inactive since the keys were read
                if (registration == null || !registration.IsActive)
                {
                    continue;
                }

                database["read_only_connection"] = registration.ReadOnlyConnectionString == null ? null : DescribeConnectionString(registration.ReadOnlyConnectionString);
                database["write_connection"] = registration.WriteConnectionString == null ? null : DescribeConnectionString(registration.WriteConnectionString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ListDatabases failed to read {DatabaseKey}: {Message}", key, ex.Message);

                database["error"] = ex.Message;
            }

            databases.Add(database);
        }

        return new DbOperationResult(success: true, data: databases);
    }
}
