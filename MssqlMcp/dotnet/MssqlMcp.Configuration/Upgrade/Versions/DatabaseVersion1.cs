// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration.Upgrade.Versions;

/// <summary>
/// Revision 1: creates the SQLServerDatabase table.
/// </summary>
public class DatabaseVersion1 : IDatabaseVersion
{
    private const string CreateTableSql =
        "CREATE TABLE IF NOT EXISTS SQLServerDatabase (" +
        "  SQLServerDatabaseID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
        "  DatabaseKey TEXT NOT NULL UNIQUE, " +
        "  ReadOnlyConnectionString TEXT, " +
        "  WriteConnectionString TEXT)";

    /// <inheritdoc/>
    public int RevisionNumber => 1;

    /// <inheritdoc/>
    public async Task UpgradeAsync(DatabaseUpgradeService upgradeService)
    {
        // "IF NOT EXISTS": configuration databases created before revisions were tracked already have the table
        await upgradeService.ExecuteAsync(CreateTableSql);
    }
}
