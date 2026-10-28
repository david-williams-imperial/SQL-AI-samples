// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Data;
using Microsoft.Data.SqlClient;

namespace Mssql.McpServer;

/// <summary>
/// Factory for <see cref="SqlConnection"/>.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with write permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database to connect to.</param>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    Task<SqlConnection> GetOpenConnectionAsync(string databaseKey);

    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with read-only permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database to connect to.</param>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    Task<SqlConnection> GetOpenReadOnlyConnectionAsync(string databaseKey);

    /// <summary>
    /// Get connection string for connection with write permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>Connection string from the database registry.</returns>
    /// <exception cref="InvalidOperationException">If the key is not configured, or has no write connection string.</exception>
    Task<string> GetConnectionStringAsync(string databaseKey);

    /// <summary>
    /// Get connection string for connection with read-only permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>Connection string from the database registry.</returns>
    /// <exception cref="InvalidOperationException">If the key is not configured, or has no read-only connection string.</exception>
    Task<string> GetReadOnlyConnectionStringAsync(string databaseKey);
}
