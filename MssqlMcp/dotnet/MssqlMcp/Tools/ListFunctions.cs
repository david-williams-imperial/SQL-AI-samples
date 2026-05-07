// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    private const string ListFunctionsQuery =
        "SELECT SCHEMA_NAME(o.schema_id) AS schema_name, o.name, " +
        "       CASE o.type " +
        "           WHEN 'FN' THEN 'Scalar' " +
        "           WHEN 'IF' THEN 'InlineTableValued' " +
        "           WHEN 'TF' THEN 'TableValued' " +
        "       END AS function_type " +
        "FROM sys.objects o " +
        "WHERE o.type IN ('FN', 'IF', 'TF') " +
        "ORDER BY schema_name, o.name";

    [McpServerTool(
        Title = "List Functions",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists all user-defined functions (scalar, inline table-valued, and multi-statement table-valued) in the SQL Database.")]
    public async Task<DbOperationResult> ListFunctions()
    {
        SqlConnection connection;

        try
        {
            connection = await _connectionFactory.GetOpenReadOnlyConnectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: $"Failed to connect to database: {ex.Message}");
        }

        try
        {
            await using (connection)
            {
                await using (SqlCommand listFunctionsCommand = connection.CreateCommand())
                {
                    listFunctionsCommand.CommandText = ListFunctionsQuery;

                    List<Dictionary<string, object>> functions = new List<Dictionary<string, object>>();

                    await using (SqlDataReader reader = await listFunctionsCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Dictionary<string, object> function = new Dictionary<string, object>
                            {
                                ["name"] = $"{reader.GetString(0)}.{reader.GetString(1)}",
                                ["type"] = reader.GetString(2)
                            };

                            functions.Add(function);
                        }
                    }

                    return new DbOperationResult(success: true, data: functions);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListFunctions failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
