// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration.Upgrade.Versions;

/// <summary>
/// Revision 2: adds the IsActive column, so databases can be temporarily disabled (existing databases are active),
/// the EnvironmentName column, so databases can be tagged as "Dev", "Live", etc. (existing databases are "Unspecified"),
/// and the Description column, describing each database to the agent (existing databases have a blank description).
/// </summary>
public class DatabaseVersion2 : IDatabaseVersion
{
    private const string TableName = "SQLServerDatabase";

    private const string AddIsActiveColumnSql = "ALTER TABLE SQLServerDatabase ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1";

    private const string AddEnvironmentNameColumnSql =
        "ALTER TABLE SQLServerDatabase ADD COLUMN EnvironmentName TEXT NOT NULL DEFAULT '" + DatabaseSummary.UnspecifiedEnvironmentName + "'";

    private const string AddDescriptionColumnSql = "ALTER TABLE SQLServerDatabase ADD COLUMN Description TEXT NOT NULL DEFAULT ''";

    /// <inheritdoc/>
    public int RevisionNumber => 2;

    /// <inheritdoc/>
    public async Task UpgradeAsync(DatabaseUpgradeService upgradeService)
    {
        // Configuration databases created before revisions were tracked may already have the IsActive column
        if (!await upgradeService.ColumnExistsAsync(TableName, "IsActive"))
        {
            await upgradeService.ExecuteAsync(AddIsActiveColumnSql);
        }

        if (!await upgradeService.ColumnExistsAsync(TableName, "EnvironmentName"))
        {
            await upgradeService.ExecuteAsync(AddEnvironmentNameColumnSql);
        }

        if (!await upgradeService.ColumnExistsAsync(TableName, "Description"))
        {
            await upgradeService.ExecuteAsync(AddDescriptionColumnSql);
        }
    }
}
