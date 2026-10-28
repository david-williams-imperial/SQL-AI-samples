// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.Sqlite;

namespace Mssql.McpServer.Configuration.Upgrade;

/// <summary>
/// Brings configuration databases up to the latest schema revision.
/// </summary>
public class DatabaseRevisionManager
{
    /// <summary>
    /// Schema revisions, in order.
    /// </summary>
    private readonly IReadOnlyList<IDatabaseVersion> _databaseVersions;

    /// <summary>
    /// Initialises a new instance of the <see cref="DatabaseRevisionManager"/> class.
    /// </summary>
    /// <param name="databaseVersions">Schema revisions. Revision numbers must run from 1 with no gaps or duplicates.</param>
    /// <exception cref="InvalidOperationException">If the revision numbers are not 1, 2, 3, and so on.</exception>
    public DatabaseRevisionManager(IEnumerable<IDatabaseVersion> databaseVersions)
    {
        _databaseVersions = databaseVersions.OrderBy(v => v.RevisionNumber).ToList();

        for (int i = 0; i < _databaseVersions.Count; i++)
        {
            if (_databaseVersions[i].RevisionNumber != i + 1)
            {
                throw new InvalidOperationException(
                    $"Database revision numbers must run from 1 with no gaps or duplicates, but found {string.Join(", ", _databaseVersions.Select(v => v.RevisionNumber))}.");
            }
        }
    }

    /// <summary>
    /// Gets the latest schema revision.
    /// </summary>
    public int LatestRevisionNumber => _databaseVersions.Count;

    /// <summary>
    /// Creates a revision manager with every <see cref="IDatabaseVersion"/> implementation in this assembly.
    /// </summary>
    /// <returns>Revision manager.</returns>
    public static DatabaseRevisionManager CreateDefault()
    {
        Type versionInterfaceType = typeof(IDatabaseVersion);

        IEnumerable<IDatabaseVersion> versions = versionInterfaceType.Assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && versionInterfaceType.IsAssignableFrom(t))
            .Select(t => Activator.CreateInstance(t) as IDatabaseVersion)
            .OfType<IDatabaseVersion>();

        return new DatabaseRevisionManager(versions);
    }

    /// <summary>
    /// Applies any schema revisions the configuration database does not have yet. Nothing is written if it is up to date.
    /// Outstanding revisions are applied in a single transaction, so a failed upgrade leaves the database unchanged,
    /// and two programs opening the same database at once cannot both apply them.
    /// </summary>
    /// <param name="connection">Open read/write connection to the configuration database.</param>
    /// <returns>Revision numbers applied, in order; empty if the database was already up to date.</returns>
    /// <exception cref="InvalidOperationException">If the configuration database is from a newer version of the program.</exception>
    public async Task<IReadOnlyList<int>> ApplyOutstandingUpgradesAsync(SqliteConnection connection)
    {
        // Check without taking a write lock first, so up-to-date databases (the usual case) are never locked
        int currentRevisionNumber = await new DatabaseUpgradeService(connection, null).GetCurrentRevisionNumberAsync();

        ThrowIfNewer(connection, currentRevisionNumber);

        if (currentRevisionNumber >= LatestRevisionNumber)
        {
            return [];
        }

        List<int> appliedRevisionNumbers = new List<int>();

        // BEGIN IMMEDIATE: waits for, then blocks, other writers until the upgrade is committed
        using (SqliteTransaction transaction = connection.BeginTransaction(deferred: false))
        {
            DatabaseUpgradeService upgradeService = new DatabaseUpgradeService(connection, transaction);

            // Another program may have upgraded the database while this one waited for the lock
            currentRevisionNumber = await upgradeService.GetCurrentRevisionNumberAsync();

            ThrowIfNewer(connection, currentRevisionNumber);

            foreach (IDatabaseVersion databaseVersion in _databaseVersions.Where(v => v.RevisionNumber > currentRevisionNumber))
            {
                await databaseVersion.UpgradeAsync(upgradeService);
                await upgradeService.SetRevisionNumberAsync(databaseVersion.RevisionNumber);

                appliedRevisionNumbers.Add(databaseVersion.RevisionNumber);
            }

            await transaction.CommitAsync();
        }

        return appliedRevisionNumbers;
    }

    /// <summary>
    /// Throws if the configuration database has a newer schema than this program knows about.
    /// </summary>
    /// <param name="connection">Connection to the configuration database, for the error message.</param>
    /// <param name="currentRevisionNumber">Revision of the configuration database.</param>
    /// <exception cref="InvalidOperationException">If the configuration database is newer.</exception>
    private void ThrowIfNewer(SqliteConnection connection, int currentRevisionNumber)
    {
        if (currentRevisionNumber > LatestRevisionNumber)
        {
            throw new InvalidOperationException(
                $"The configuration database '{connection.DataSource}' is at schema revision {currentRevisionNumber}, " +
                $"but this version of the program only supports up to revision {LatestRevisionNumber}. " +
                "Update the MCP server and configuration utility to the latest version.");
        }
    }
}
