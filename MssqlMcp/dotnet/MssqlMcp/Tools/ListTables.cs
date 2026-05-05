// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    private const string ListTablesQuery =
        "SELECT TABLE_SCHEMA, TABLE_NAME " +
        "FROM INFORMATION_SCHEMA.TABLES " +
        "WHERE TABLE_TYPE = 'BASE TABLE' " +
        "ORDER BY TABLE_SCHEMA, TABLE_NAME";

    [McpServerTool(
        Title = "List Tables",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists all tables in the SQL Database.")]
    public async Task<DbOperationResult> ListTables()
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
                await using (SqlCommand listTablesCommand = connection.CreateCommand())
                {
                    listTablesCommand.CommandText = ListTablesQuery;

                    List<string> tables = new List<string>();

                    await using (SqlDataReader reader = await listTablesCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            tables.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
                        }
                    }

                    return new DbOperationResult(success: true, data: tables);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListTables failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
