// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.Sqlite;
using Mssql.McpServer;

namespace MssqlMcp.Tests
{
    /// <summary>
    /// Tests for <see cref="SqliteDatabaseRegistry"/>, using a temporary configuration database.
    /// </summary>
    public sealed class SqliteDatabaseRegistryTests : IDisposable
    {
        private const string ReadOnlyConnectionString = "Server=.;Database=test;User ID=reader;Password=ReadSecret!";

        private const string WriteConnectionString = "Server=.;Database=test;User ID=writer;Password=WriteSecret!";

        /// <summary>
        /// Temporary folder holding the configuration database.
        /// </summary>
        private readonly string _folder;

        /// <summary>
        /// Registry under test.
        /// </summary>
        private readonly SqliteDatabaseRegistry _registry;

        /// <summary>
        /// Initialises the registry with a configuration database path that does not exist yet.
        /// </summary>
        public SqliteDatabaseRegistryTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), $"MssqlMcpTests_{Guid.NewGuid():N}");
            _registry = new SqliteDatabaseRegistry(Path.Combine(_folder, "Config.db"));
        }

        /// <summary>
        /// Deletes the temporary folder.
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }

        [Fact]
        public async Task GetDatabaseKeys_Throws_WhenConfigurationDatabaseDoesNotExist()
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _registry.GetDatabaseKeysAsync(activeOnly: false));

            Assert.Contains(SqliteDatabaseRegistry.PathEnvironmentVariable, ex.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(_registry.DatabasePath));
        }

        [Fact]
        public async Task SetDatabase_CreatesConfigurationDatabase_AndRoundTrips()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, WriteConnectionString);

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.NotNull(registration);
            Assert.Equal("main", registration.DatabaseKey);
            Assert.Equal(ReadOnlyConnectionString, registration.ReadOnlyConnectionString);
            Assert.Equal(WriteConnectionString, registration.WriteConnectionString);
        }

        [Fact]
        public async Task SetDatabase_StoresConnectionStringsEncrypted()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, WriteConnectionString);

            List<string> storedValues = await ReadStoredConnectionStringsAsync();

            Assert.Equal(2, storedValues.Count);
            Assert.All(storedValues, v => Assert.DoesNotContain("Secret!", v, StringComparison.Ordinal));
        }

        [Fact]
        public async Task SetDatabase_StoresNull_WhenWriteConnectionStringIsBlank()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, " ");

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.NotNull(registration);
            Assert.Null(registration.WriteConnectionString);
        }

        [Fact]
        public async Task SetDatabase_ReplacesExistingKey()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, WriteConnectionString);
            await _registry.SetDatabaseAsync("main", WriteConnectionString, null);

            IReadOnlyList<string> keys = await _registry.GetDatabaseKeysAsync(activeOnly: false);
            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.Single(keys);
            Assert.NotNull(registration);
            Assert.Equal(WriteConnectionString, registration.ReadOnlyConnectionString);
            Assert.Null(registration.WriteConnectionString);
        }

        [Fact]
        public async Task GetDatabaseKeys_ReturnsKeysInOrder()
        {
            await _registry.SetDatabaseAsync("zeta", ReadOnlyConnectionString, null);
            await _registry.SetDatabaseAsync("alpha", ReadOnlyConnectionString, null);

            IReadOnlyList<string> keys = await _registry.GetDatabaseKeysAsync(activeOnly: false);

            Assert.Equal(["alpha", "zeta"], keys);
        }

        [Fact]
        public async Task GetDatabase_ReturnsNull_WhenKeyIsNotConfigured()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null);

            Assert.Null(await _registry.GetDatabaseAsync("other"));
        }

        [Fact]
        public async Task RemoveDatabase_RemovesKey()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null);

            Assert.True(await _registry.RemoveDatabaseAsync("main"));
            Assert.False(await _registry.RemoveDatabaseAsync("main"));
            Assert.Empty(await _registry.GetDatabaseKeysAsync(activeOnly: false));
        }

        [Fact]
        public async Task AddDatabase_CreatesConfigurationDatabase_AndRoundTrips()
        {
            Assert.False(_registry.ConfigurationDatabaseExists);

            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, WriteConnectionString, true));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.True(_registry.ConfigurationDatabaseExists);
            Assert.NotNull(registration);
            Assert.Equal(ReadOnlyConnectionString, registration.ReadOnlyConnectionString);
            Assert.Equal(WriteConnectionString, registration.WriteConnectionString);
        }

        [Fact]
        public async Task AddDatabase_Throws_WhenKeyExists()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _registry.AddDatabaseAsync(new DatabaseRegistration("main", WriteConnectionString, null, true)));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.Contains("already exists", ex.Message, StringComparison.Ordinal);
            Assert.NotNull(registration);
            Assert.Equal(ReadOnlyConnectionString, registration.ReadOnlyConnectionString);
        }

        [Fact]
        public async Task UpdateDatabase_RenamesKey_AndReplacesConnectionStrings()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, WriteConnectionString, true));

            bool updated = await _registry.UpdateDatabaseAsync("main", new DatabaseRegistration("renamed", WriteConnectionString, null, true));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("renamed");

            Assert.True(updated);
            Assert.Equal(["renamed"], await _registry.GetDatabaseKeysAsync(activeOnly: false));
            Assert.NotNull(registration);
            Assert.Equal(WriteConnectionString, registration.ReadOnlyConnectionString);
            Assert.Null(registration.WriteConnectionString);
        }

        [Fact]
        public async Task UpdateDatabase_ReturnsFalse_WhenKeyIsNotConfigured()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true));

            Assert.False(await _registry.UpdateDatabaseAsync("other", new DatabaseRegistration("other", ReadOnlyConnectionString, null, true)));
        }

        [Fact]
        public async Task UpdateDatabase_Throws_WhenRenamedToExistingKey()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("first", ReadOnlyConnectionString, null, true));
            await _registry.AddDatabaseAsync(new DatabaseRegistration("second", ReadOnlyConnectionString, null, true));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _registry.UpdateDatabaseAsync("second", new DatabaseRegistration("first", ReadOnlyConnectionString, null, true)));

            Assert.Equal(["first", "second"], await _registry.GetDatabaseKeysAsync(activeOnly: false));
        }

        [Fact]
        public async Task InactiveDatabase_IsExcludedFromActiveKeys_ButStillReadable()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("active", ReadOnlyConnectionString, null, true));
            await _registry.AddDatabaseAsync(new DatabaseRegistration("inactive", ReadOnlyConnectionString, null, false));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("inactive");

            Assert.Equal(["active"], await _registry.GetDatabaseKeysAsync(activeOnly: true));
            Assert.Equal(["active", "inactive"], await _registry.GetDatabaseKeysAsync(activeOnly: false));
            Assert.NotNull(registration);
            Assert.False(registration.IsActive);
        }

        [Fact]
        public async Task UpdateDatabase_ChangesActiveState()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true));
            await _registry.UpdateDatabaseAsync("main", new DatabaseRegistration("main", ReadOnlyConnectionString, null, false));

            Assert.Empty(await _registry.GetDatabaseKeysAsync(activeOnly: true));

            await _registry.UpdateDatabaseAsync("main", new DatabaseRegistration("main", ReadOnlyConnectionString, null, true));

            Assert.Equal(["main"], await _registry.GetDatabaseKeysAsync(activeOnly: true));
        }

        [Fact]
        public async Task SetDatabase_AddsActiveDatabase_AndKeepsExistingActiveState()
        {
            await _registry.SetDatabaseAsync("new", ReadOnlyConnectionString, null);
            await _registry.AddDatabaseAsync(new DatabaseRegistration("inactive", ReadOnlyConnectionString, null, false));
            await _registry.SetDatabaseAsync("inactive", WriteConnectionString, null);

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("inactive");

            Assert.Equal(["new"], await _registry.GetDatabaseKeysAsync(activeOnly: true));
            Assert.NotNull(registration);
            Assert.False(registration.IsActive);
            Assert.Equal(WriteConnectionString, registration.ReadOnlyConnectionString);
        }

        [Fact]
        public async Task AddDatabase_SavesTrimmedEnvironment()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true, " Live "));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");
            DatabaseSummary summary = Assert.Single(await _registry.GetDatabaseSummariesAsync(activeOnly: true));

            Assert.NotNull(registration);
            Assert.Equal("Live", registration.EnvironmentName);
            Assert.Equal("Live", summary.EnvironmentName);
        }

        [Fact]
        public async Task AddDatabase_SavesBlankEnvironmentAsUnspecified()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true, string.Empty));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.NotNull(registration);
            Assert.Equal(DatabaseSummary.UnspecifiedEnvironmentName, registration.EnvironmentName);
        }

        [Fact]
        public async Task UpdateDatabase_ChangesEnvironment()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true, "Dev"));
            await _registry.UpdateDatabaseAsync("main", new DatabaseRegistration("main", ReadOnlyConnectionString, null, true, "Live"));

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.NotNull(registration);
            Assert.Equal("Live", registration.EnvironmentName);
        }

        [Fact]
        public async Task SetDatabase_DefaultsToUnspecified_AndKeepsEnvironmentUnlessGiven()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null);

            Assert.Equal(DatabaseSummary.UnspecifiedEnvironmentName, (await _registry.GetDatabaseAsync("main"))?.EnvironmentName);

            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null, "Live");
            await _registry.SetDatabaseAsync("main", WriteConnectionString, null, " ");

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.NotNull(registration);
            Assert.Equal("Live", registration.EnvironmentName);
            Assert.Equal(WriteConnectionString, registration.ReadOnlyConnectionString);
        }

        [Fact]
        public async Task GetDatabaseSummaries_ReturnsDetails_WhenConnectionStringsCannotBeDecrypted()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, false, "Live"));
            await ExecuteSqlAsync($"UPDATE SQLServerDatabase SET ReadOnlyConnectionString = '{ReadOnlyConnectionString}'");

            DatabaseSummary summary = Assert.Single(await _registry.GetDatabaseSummariesAsync(activeOnly: false));

            Assert.Equal("main", summary.DatabaseKey);
            Assert.False(summary.IsActive);
            Assert.Equal("Live", summary.EnvironmentName);
            Assert.Empty(await _registry.GetDatabaseSummariesAsync(activeOnly: true));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _registry.GetDatabaseAsync("main"));
        }

        [Fact]
        public async Task AddAndUpdateDatabase_SaveTrimmedDescription()
        {
            await _registry.AddDatabaseAsync(new DatabaseRegistration("main", ReadOnlyConnectionString, null, true, "Live", " Main 3sixty database "));

            DatabaseRegistration? added = await _registry.GetDatabaseAsync("main");

            await _registry.UpdateDatabaseAsync("main", new DatabaseRegistration("main", ReadOnlyConnectionString, null, true, "Live", "Renamed"));

            DatabaseSummary summary = Assert.Single(await _registry.GetDatabaseSummariesAsync(activeOnly: true));

            Assert.NotNull(added);
            Assert.Equal("Main 3sixty database", added.Description);
            Assert.Equal("Renamed", summary.Description);
        }

        [Fact]
        public async Task SetDatabase_DefaultsToBlankDescription_AndKeepsDescriptionUnlessGiven()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null);

            Assert.Equal(string.Empty, (await _registry.GetDatabaseAsync("main"))?.Description);

            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null, null, "Main 3sixty database");
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null, "Live", null);

            DatabaseRegistration? registration = await _registry.GetDatabaseAsync("main");

            Assert.NotNull(registration);
            Assert.Equal("Main 3sixty database", registration.Description);
            Assert.Equal("Live", registration.EnvironmentName);
        }

        [Fact]
        public async Task GetDatabase_Throws_WhenConnectionStringIsPlainText()
        {
            await _registry.SetDatabaseAsync("main", ReadOnlyConnectionString, null);
            await ExecuteSqlAsync($"UPDATE SQLServerDatabase SET ReadOnlyConnectionString = '{ReadOnlyConnectionString}'");

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _registry.GetDatabaseAsync("main"));

            Assert.Contains("set-database main", ex.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads the raw connection string values stored in the configuration database.
        /// </summary>
        /// <returns>Non-null stored values.</returns>
        private async Task<List<string>> ReadStoredConnectionStringsAsync()
        {
            List<string> values = new List<string>();

            await using (SqliteConnection connection = OpenConnection())
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT ReadOnlyConnectionString, WriteConnectionString FROM SQLServerDatabase";

                    await using (SqliteDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                if (!reader.IsDBNull(i))
                                {
                                    values.Add(reader.GetString(i));
                                }
                            }
                        }
                    }
                }
            }

            return values;
        }

        /// <summary>
        /// Runs SQL directly against the configuration database.
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
            SqliteConnection connection = new SqliteConnection($"Data Source={_registry.DatabasePath};Pooling=False");

            connection.Open();

            return connection;
        }
    }
}
