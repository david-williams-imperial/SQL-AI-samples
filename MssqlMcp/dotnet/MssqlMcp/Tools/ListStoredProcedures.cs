// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    private const string ListStoredProceduresQuery =
        "SELECT ROUTINE_SCHEMA, ROUTINE_NAME " +
        "FROM INFORMATION_SCHEMA.ROUTINES " +
        "WHERE ROUTINE_TYPE = 'PROCEDURE' " +
        "ORDER BY ROUTINE_SCHEMA, ROUTINE_NAME";

    [McpServerTool(
        Title = "List Stored Procedures",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists all stored procedures in the SQL Database.")]
    public async Task<DbOperationResult> ListStoredProcedures()
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
                await using (SqlCommand listStoredProceduresCommand = connection.CreateCommand())
                {
                    listStoredProceduresCommand.CommandText = ListStoredProceduresQuery;

                    List<string> storedProcedures = new List<string>();

                    await using (SqlDataReader reader = await listStoredProceduresCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            storedProcedures.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
                        }
                    }

                    return new DbOperationResult(success: true, data: storedProcedures);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListStoredProcedures failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
