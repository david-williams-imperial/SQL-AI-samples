// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
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
        [Description(DatabaseKeyDescription)] string databaseKey,
        [Description("INSERT SQL statement")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DbOperationResult(success: false, error: "SQL parameter cannot be null/empty/whitespace");
        }

        return await ExecuteAsync(databaseKey, readOnly: false, nameof(InsertData), async connection =>
        {
            await using (SqlCommand insertCommand = connection.CreateCommand())
            {
                insertCommand.CommandText = sql;

                int rows = await insertCommand.ExecuteNonQueryAsync();

                return new DbOperationResult(success: true, rowsAffected: rows);
            }
        });
    }
}
