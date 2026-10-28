// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration.Upgrade;

/// <summary>
/// A schema revision of the configuration database. Implementations are discovered automatically and applied in
/// order of <see cref="RevisionNumber"/>, which must run from 1 with no gaps.
/// </summary>
/// <remarks>
/// Configuration databases created before revisions were tracked report revision 0, but may already contain some of
/// the changes, so revisions 1 and 2 check before changing anything. Later revisions can assume earlier ones applied.
/// </remarks>
public interface IDatabaseVersion
{
    /// <summary>
    /// Gets the revision number this upgrade brings the configuration database to.
    /// </summary>
    int RevisionNumber { get; }

    /// <summary>
    /// Applies the schema changes for this revision.
    /// </summary>
    /// <param name="upgradeService">Service for inspecting and changing the configuration database, within the upgrade transaction.</param>
    /// <returns>A task that completes when the changes have been applied.</returns>
    Task UpgradeAsync(DatabaseUpgradeService upgradeService);
}
