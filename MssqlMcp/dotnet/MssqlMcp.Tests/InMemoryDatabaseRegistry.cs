// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Mssql.McpServer;

namespace MssqlMcp.Tests
{
    /// <summary>
    /// Database registry holding a fixed set of databases in memory.
    /// </summary>
    public sealed class InMemoryDatabaseRegistry : IDatabaseRegistry
    {
        /// <summary>
        /// Databases, by key.
        /// </summary>
        private readonly Dictionary<string, DatabaseRegistration> _databases;

        /// <summary>
        /// Initialises a new instance of the <see cref="InMemoryDatabaseRegistry"/> class.
        /// </summary>
        /// <param name="databases">Databases to register.</param>
        public InMemoryDatabaseRegistry(params DatabaseRegistration[] databases)
        {
            _databases = databases.ToDictionary(d => d.DatabaseKey);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetDatabaseKeysAsync(bool activeOnly)
        {
            IReadOnlyList<string> keys = _databases.Values
                .Where(d => d.IsActive || !activeOnly)
                .Select(d => d.DatabaseKey)
                .Order()
                .ToList();

            return Task.FromResult(keys);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<DatabaseSummary>> GetDatabaseSummariesAsync(bool activeOnly)
        {
            IReadOnlyList<DatabaseSummary> summaries = _databases.Values
                .Where(d => d.IsActive || !activeOnly)
                .OrderBy(d => d.DatabaseKey, StringComparer.Ordinal)
                .Select(d => new DatabaseSummary(d.DatabaseKey, d.IsActive, d.EnvironmentName, d.Description))
                .ToList();

            return Task.FromResult(summaries);
        }

        /// <inheritdoc/>
        public Task<DatabaseRegistration?> GetDatabaseAsync(string databaseKey)
        {
            DatabaseRegistration? registration = _databases.GetValueOrDefault(databaseKey);

            return Task.FromResult(registration);
        }
    }
}
