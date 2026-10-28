// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.Sqlite;
using Mssql.McpServer.Configuration.Upgrade;

namespace MssqlMcp.Tests
{
    /// <summary>
    /// Tests for upgrading the schema of configuration databases when they are first opened.
    /// </summary>
    public sealed class DatabaseUpgradeTests : IDisposable
    {
        private const string ConnectionString = "Server=.;Database=test;User ID=reader;Password=ReadSecret!";

        /// <summary>
        /// Schema created by the first versions of the configuration utility, before revisions were tracked.
        /// </summary>
        private const string OriginalSchemaSql =
            "CREATE TABLE SQLServerDatabase (SQLServerDatabaseID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
            "DatabaseKey TEXT NOT NULL UNIQUE, ReadOnlyConnectionString TEXT, WriteConnectionString TEXT)";

        /// <summary>
        /// Temporary folder holding the configuration database.
        /// </summary>
        private readonly string _folder;

        /// <summary>
        /// Path of the configuration database.
        /// </summary>
        private readonly string _databasePath;

        /// <summary>
        /// Initialises a configuration database path that does not exist yet.
        /// </summary>
        public DatabaseUpgradeTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), $"MssqlMcpTests_{Guid.NewGuid():N}");
            _databasePath = Path.Combine(_folder, "Config.db");

            Directory.CreateDirectory(_folder);
        }

        /// <summary>
        /// Deletes the temporary folder.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(_databasePath))
            {
                File.SetAttributes(_databasePath, FileAttributes.Normal);
            }

            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public void DefaultRevisionManager_HasSequentialRevisions()
        {
            DatabaseRevisionManager manager = DatabaseRevisionManager.CreateDefault();

            Assert.Equal(2, manager.LatestRevisionNumber);
            Assert.Equal(manager.LatestRevisionNumber, SqliteDatabaseRegistry.LatestRevisionNumber);
        }

        [Theory]
        [InlineData(1, 3)]
        [InlineData(1, 1)]
        [InlineData(2, 3)]
        public void RevisionManager_Throws_WhenRevisionNumbersAreNotSequential(int first, int second)
        {
            TestDatabaseVersion[] versions = [new TestDatabaseVersion(first, _ => Task.CompletedTask), new TestDatabaseVersion(second, _ => Task.CompletedTask)];

            Assert.Throws<InvalidOperationException>(() => new DatabaseRevisionManager(versions));
        }

        [Fact]
        public async Task NewConfigurationDatabase_IsCreatedAtLatestRevision()
        {
            SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(_databasePath);

            await registry.AddDatabaseAsync(new DatabaseRegistration("main", ConnectionString, null, false));

            Assert.Equal([1, 2], await GetRevisionHistoryAsync());
            Assert.Equal(["main"], await registry.GetDatabaseKeysAsync(activeOnly: false));
            Assert.Empty(await registry.GetDatabaseKeysAsync(activeOnly: true));
        }

        [Fact]
        public async Task OriginalConfigurationDatabase_IsUpgradedOnFirstRead_KeepingItsData()
        {
            await ExecuteSqlAsync(OriginalSchemaSql);
            await ExecuteSqlAsync($"INSERT INTO SQLServerDatabase (DatabaseKey, ReadOnlyConnectionString) VALUES ('legacy', '{ConnectionStringProtector.Protect(ConnectionString)}')");

            SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(_databasePath);

            IReadOnlyList<string> activeKeys = await registry.GetDatabaseKeysAsync(activeOnly: true);
            DatabaseRegistration? registration = await registry.GetDatabaseAsync("legacy");

            Assert.Equal(["legacy"], activeKeys);
            Assert.NotNull(registration);
            Assert.True(registration.IsActive);
            Assert.Equal(ConnectionString, registration.ReadOnlyConnectionString);
            Assert.Equal(DatabaseSummary.UnspecifiedEnvironmentName, registration.EnvironmentName);
            Assert.Equal(string.Empty, registration.Description);
            Assert.Equal([1, 2], await GetRevisionHistoryAsync());
        }

        [Fact]
        public async Task ConfigurationDatabaseWithIsActiveButNoHistory_IsUpgradedWithoutError()
        {
            await ExecuteSqlAsync(OriginalSchemaSql);
            await ExecuteSqlAsync("ALTER TABLE SQLServerDatabase ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1");
            await ExecuteSqlAsync($"INSERT INTO SQLServerDatabase (DatabaseKey, ReadOnlyConnectionString, IsActive) VALUES ('disabled', '{ConnectionStringProtector.Protect(ConnectionString)}', 0)");

            SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(_databasePath);

            IReadOnlyList<int> applied = await registry.UpgradeAsync();

            Assert.Equal([1, 2], applied);
            Assert.Empty(await registry.GetDatabaseKeysAsync(activeOnly: true));
            Assert.Equal(["disabled"], await registry.GetDatabaseKeysAsync(activeOnly: false));
            Assert.Equal(DatabaseSummary.UnspecifiedEnvironmentName, Assert.Single(await registry.GetDatabaseSummariesAsync(activeOnly: false)).EnvironmentName);
        }

        [Fact]
        public async Task Upgrade_RunsOnlyOnFirstOpen()
        {
            await ExecuteSqlAsync(OriginalSchemaSql);

            SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(_databasePath);

            Assert.Equal([1, 2], await registry.UpgradeAsync());
            Assert.Empty(await registry.UpgradeAsync());
            Assert.Empty(await new SqliteDatabaseRegistry(_databasePath).UpgradeAsync());
            Assert.Equal([1, 2], await GetRevisionHistoryAsync());
        }

        [Fact]
        public async Task UpToDateConfigurationDatabase_CanBeReadWhenFileIsReadOnly()
        {
            await new SqliteDatabaseRegistry(_databasePath).AddDatabaseAsync(new DatabaseRegistration("main", ConnectionString, null, true));

            File.SetAttributes(_databasePath, FileAttributes.ReadOnly);

            SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(_databasePath);

            Assert.Equal(["main"], await registry.GetDatabaseKeysAsync(activeOnly: true));
        }

        [Fact]
        public async Task NewerConfigurationDatabase_IsRefused_AndLeftUnchanged()
        {
            await ExecuteSqlAsync(OriginalSchemaSql);
            await ExecuteSqlAsync(
                "CREATE TABLE DatabaseRevisionHistory (DatabaseRevisionHistoryID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, RevisionNumber INTEGER NOT NULL, TimeAppliedUTC TEXT NOT NULL); " +
                "INSERT INTO DatabaseRevisionHistory (RevisionNumber, TimeAppliedUTC) VALUES (99, '2030-01-01 00:00:00')");

            SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(_databasePath);

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => registry.GetDatabaseKeysAsync(activeOnly: false));

            Assert.Contains("revision 99", ex.Message, StringComparison.Ordinal);
            Assert.Equal([99], await GetRevisionHistoryAsync());
            Assert.False(await ColumnExistsAsync("SQLServerDatabase", "IsActive"));
        }

        [Fact]
        public async Task ConcurrentFirstOpens_ApplyEachRevisionOnce()
        {
            await ExecuteSqlAsync(OriginalSchemaSql);

            IEnumerable<Task<IReadOnlyList<string>>> reads = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => new SqliteDatabaseRegistry(_databasePath).GetDatabaseKeysAsync(activeOnly: true)));

            await Task.WhenAll(reads);

            Assert.Equal([1, 2], await GetRevisionHistoryAsync());
            Assert.True(await ColumnExistsAsync("SQLServerDatabase", "IsActive"));
        }

        [Fact]
        public async Task FailedUpgrade_IsRolledBack()
        {
            DatabaseRevisionManager manager = new DatabaseRevisionManager([
                new TestDatabaseVersion(1, s => s.ExecuteAsync("CREATE TABLE Probe (ProbeID INTEGER)")),
                new TestDatabaseVersion(2, _ => throw new InvalidOperationException("Upgrade failed"))]);

            await using (SqliteConnection connection = OpenConnection())
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ApplyOutstandingUpgradesAsync(connection));

                DatabaseUpgradeService upgradeService = new DatabaseUpgradeService(connection, null);

                Assert.False(await upgradeService.TableExistsAsync("Probe"));
                Assert.Equal(0, await upgradeService.GetCurrentRevisionNumberAsync());
            }
        }

        /// <summary>
        /// Gets the revision numbers recorded in the configuration database.
        /// </summary>
        /// <returns>Revision numbers, in order.</returns>
        private async Task<List<int>> GetRevisionHistoryAsync()
        {
            List<int> revisions = new List<int>();

            await using (SqliteConnection connection = OpenConnection())
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT RevisionNumber FROM DatabaseRevisionHistory ORDER BY RevisionNumber";

                    await using (SqliteDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            revisions.Add(reader.GetInt32(0));
                        }
                    }
                }
            }

            return revisions;
        }

        /// <summary>
        /// Checks whether a column exists in the configuration database.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="columnName">Name of the column.</param>
        /// <returns>True if the column exists.</returns>
        private async Task<bool> ColumnExistsAsync(string tableName, string columnName)
        {
            await using (SqliteConnection connection = OpenConnection())
            {
                return await new DatabaseUpgradeService(connection, null).ColumnExistsAsync(tableName, columnName);
            }
        }

        /// <summary>
        /// Runs SQL directly against the configuration database, creating it if needed.
        /// </summary>
        /// <param name="sql">SQL to run.</param>
        /// <returns>A task that completes when the SQL has run.</returns>
        private async Task ExecuteSqlAsync(string sql)
        {
            await using (SqliteConnection connection = OpenConnection())
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Opens an unpooled connection to the configuration database, so the file can be deleted afterwards.
        /// </summary>
        /// <returns>Open connection.</returns>
        private SqliteConnection OpenConnection()
        {
            SqliteConnection connection = new SqliteConnection($"Data Source={_databasePath};Pooling=False");

            connection.Open();

            return connection;
        }
    }
}
