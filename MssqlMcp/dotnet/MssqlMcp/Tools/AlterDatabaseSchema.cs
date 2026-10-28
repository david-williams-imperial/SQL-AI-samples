// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Alter Database Schema",
        ReadOnly = false,
        Destructive = true),
        Description("Can be used to create, alter, or drop tables, indexes, views, etc. Expects a valid ALTER TABLE (or similar) SQL statement as input.")]
    public async Task<DbOperationResult> AlterDatabaseSchema(
        [Description(DatabaseKeyDescription)] string databaseKey,
        [Description("SQL statement")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DbOperationResult(success: false, error: "SQL parameter cannot be null/empty/whitespace");
        }

        return await ExecuteAsync(databaseKey, readOnly: false, nameof(AlterDatabaseSchema), async connection =>
        {
            await using (SqlCommand alterCommand = connection.CreateCommand())
            {
                alterCommand.CommandText = sql;

                int rowsAffected = await alterCommand.ExecuteNonQueryAsync();

                return new DbOperationResult(success: true, rowsAffected: rowsAffected);
            }
        });
    }
}
