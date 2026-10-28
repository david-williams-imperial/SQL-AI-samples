// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Update Data",
        ReadOnly = false,
        Destructive = true),
        Description("Updates data in a table in the SQL Database. Expects a valid UPDATE SQL statement as input.")]
    public async Task<DbOperationResult> UpdateData(
        [Description(DatabaseKeyDescription)] string databaseKey,
        [Description("UPDATE SQL statement")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DbOperationResult(success: false, error: "SQL parameter cannot be null/empty/whitespace");
        }

        return await ExecuteAsync(databaseKey, readOnly: false, nameof(UpdateData), async connection =>
        {
            await using (SqlCommand updateCommand = connection.CreateCommand())
            {
                updateCommand.CommandText = sql;

                int rows = await updateCommand.ExecuteNonQueryAsync();

                return new DbOperationResult(success: true, rowsAffected: rows);
            }
        });
    }
}
