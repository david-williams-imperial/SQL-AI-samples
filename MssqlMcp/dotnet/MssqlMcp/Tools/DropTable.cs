// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Drop Table",
        ReadOnly = false,
        Destructive = true),
        Description(
            "Drops a table in the SQL Server Database. " +
            "For more complex schema changes (e.g. dropping individual columns, indexes, or constraints), use the AlterTable tool instead.")]
    public async Task<DbOperationResult> DropTable(
        [Description("Name of the table to drop, optionally prefixed with the schema (e.g. dbo.mytable)")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new DbOperationResult(success: false, error: "Table name parameter cannot be null/empty/whitespace");
        }

        string? schema = null;

        if (name.Contains('.'))
        {
            string[] parts = name.Split('.', 2);
            schema = parts[0];
            name = parts[1];
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
                await using (SqlCommand checkTableCommand = connection.CreateCommand())
                {
                    checkTableCommand.CommandText =
                        "SELECT 1 FROM sys.tables t " +
                        "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
                        "WHERE t.name = @TableName AND (s.name = @TableSchema OR @TableSchema IS NULL)";

                    checkTableCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    checkTableCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    object? exists = await checkTableCommand.ExecuteScalarAsync();

                    if (exists == null)
                    {
                        return new DbOperationResult(success: false, error: $"Table '{name}' not found.");
                    }
                }

                string qualifiedName = schema == null
                    ? $"[{name.Replace("]", "]]")}]"
                    : $"[{schema.Replace("]", "]]")}].[{name.Replace("]", "]]")}]";

                await using (SqlCommand dropTableCommand = connection.CreateCommand())
                {
                    dropTableCommand.CommandText = $"DROP TABLE {qualifiedName}";

                    await dropTableCommand.ExecuteNonQueryAsync();

                    return new DbOperationResult(success: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DropTable failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
