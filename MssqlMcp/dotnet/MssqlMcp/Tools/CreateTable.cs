// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Create Table",
        ReadOnly = false,
        Destructive = false),
        Description("Creates a new table in the SQL Database. Expects a valid CREATE TABLE SQL statement as input.")]
    public async Task<DbOperationResult> CreateTable(
        [Description(DatabaseKeyDescription)] string databaseKey,
        [Description("CREATE TABLE SQL statement")] string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new DbOperationResult(success: false, error: "SQL parameter cannot be null/empty/whitespace");
        }

        return await ExecuteAsync(databaseKey, readOnly: false, nameof(CreateTable), async connection =>
        {
            await using (SqlCommand createCommand = connection.CreateCommand())
            {
                createCommand.CommandText = sql;

                await createCommand.ExecuteNonQueryAsync();

                return new DbOperationResult(success: true);
            }
        });
    }
}
