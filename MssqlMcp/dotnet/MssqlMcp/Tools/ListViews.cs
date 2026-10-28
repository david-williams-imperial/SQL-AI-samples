// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    private const string ListViewsQuery =
        "SELECT TABLE_SCHEMA, TABLE_NAME " +
        "FROM INFORMATION_SCHEMA.VIEWS " +
        "ORDER BY TABLE_SCHEMA, TABLE_NAME";

    [McpServerTool(
        Title = "List Views",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Lists all views in the SQL Database.")]
    public async Task<DbOperationResult> ListViews(
        [Description(DatabaseKeyDescription)] string databaseKey)
    {
        return await ExecuteAsync(databaseKey, readOnly: true, nameof(ListViews), async connection =>
        {
            await using (SqlCommand listViewsCommand = connection.CreateCommand())
            {
                listViewsCommand.CommandText = ListViewsQuery;

                List<string> views = new List<string>();

                await using (SqlDataReader reader = await listViewsCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        views.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
                    }
                }

                return new DbOperationResult(success: true, data: views);
            }
        });
    }
}
