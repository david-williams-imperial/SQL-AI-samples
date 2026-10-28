// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Mssql.McpServer.Configuration.Upgrade;

namespace MssqlMcp.Tests
{
    /// <summary>
    /// Schema revision that runs a given action, for testing <see cref="DatabaseRevisionManager"/>.
    /// </summary>
    public sealed class TestDatabaseVersion(int revisionNumber, Func<DatabaseUpgradeService, Task> upgrade) : IDatabaseVersion
    {
        /// <inheritdoc/>
        public int RevisionNumber { get; } = revisionNumber;

        /// <inheritdoc/>
        public Task UpgradeAsync(DatabaseUpgradeService upgradeService)
        {
            return upgrade(upgradeService);
        }
    }
}
