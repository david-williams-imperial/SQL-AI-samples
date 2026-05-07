// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Read Data",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Executes a SQL SELECT query against the SQL Database and returns the results. " +
                     "Multiple result sets may be returned. " +
                     "Database schema information can also be queried.")]
    public async Task<DbOperationResult> ReadData(
        [Description("SQL SELECT query")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DbOperationResult(success: false, error: "SQL parameter cannot be null/empty/whitespace");
        }

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
                Dictionary<string, List<Dictionary<string, object?>>> resultSets = new Dictionary<string, List<Dictionary<string, object?>>>();

                await using (SqlCommand queryCommand = connection.CreateCommand())
                {
                    queryCommand.CommandText = sql;

                    await using (SqlDataReader reader = await queryCommand.ExecuteReaderAsync())
                    {
                        int currentResultSet = 0;

                        do
                        {
                            List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>();

                            while (await reader.ReadAsync())
                            {
                                Dictionary<string, object?> row = new Dictionary<string, object?>(reader.FieldCount);

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }

                                rows.Add(row);
                            }

                            resultSets.Add($"result_set_{currentResultSet}", rows);

                            currentResultSet++;
                        }
                        while (await reader.NextResultAsync());
                    }
                }

                return new DbOperationResult(success: true, data: resultSets);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ReadData failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
