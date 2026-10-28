// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
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
    public async Task<DbOperationResult> ListTables(
        [Description(DatabaseKeyDescription)] string databaseKey)
    {
        return await ExecuteAsync(databaseKey, readOnly: true, nameof(ListTables), async connection =>
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
        });
    }
}
