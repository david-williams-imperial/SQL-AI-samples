// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration;

/// <summary>
/// Source of the databases the MCP server can connect to.
/// </summary>
public interface IDatabaseRegistry
{
    /// <summary>
    /// Gets the keys of the configured databases.
    /// </summary>
    /// <param name="activeOnly">Return only active databases (true), or all databases (false)?</param>
    /// <returns>Database keys, in alphabetical order.</returns>
    Task<IReadOnlyList<string>> GetDatabaseKeysAsync(bool activeOnly);

    /// <summary>
    /// Gets the details of the configured databases that do not need decrypting, so cannot fail for a single database.
    /// </summary>
    /// <param name="activeOnly">Return only active databases (true), or all databases (false)?</param>
    /// <returns>Database summaries, in alphabetical order of key.</returns>
    Task<IReadOnlyList<DatabaseSummary>> GetDatabaseSummariesAsync(bool activeOnly);

    /// <summary>
    /// Gets a configured database by key, whether or not it is active.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>The database, with decrypted connection strings, or null if the key is not configured.</returns>
    /// <exception cref="InvalidOperationException">If the database's connection strings cannot be read.</exception>
    Task<DatabaseRegistration?> GetDatabaseAsync(string databaseKey);
}
