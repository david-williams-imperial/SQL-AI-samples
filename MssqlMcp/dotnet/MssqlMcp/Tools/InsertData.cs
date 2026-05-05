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
        Title = "Insert Data",
        ReadOnly = false,
        Destructive = false),
        Description("Inserts data into a table in the SQL Database. Expects a valid INSERT SQL statement as input.")]
    public async Task<DbOperationResult> InsertData(
        [Description("INSERT SQL statement")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DbOperationResult(success: false, error: "SQL parameter cannot be null/empty/whitespace");
        }

        SqlConnection connection;

        try
        {
            connection = await _connectionFactory.GetOpenConnectionAsync();
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
                await using (SqlCommand insertCommand = connection.CreateCommand())
                {
                    insertCommand.CommandText = sql;

                    int rows = await insertCommand.ExecuteNonQueryAsync();

                    return new DbOperationResult(success: true, rowsAffected: rows);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertData failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
