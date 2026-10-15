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
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    Task<SqlConnection> GetOpenConnectionAsync();

    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with read-only permissions.
    /// </summary>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    Task<SqlConnection> GetOpenReadOnlyConnectionAsync();

    /// <summary>
    /// Get connection string for connection with write permissions.
    /// </summary>
    /// <returns>Connection string from environment configuration.</returns>
    /// <exception cref="InvalidOperationException">If the environment variable 'CONNECTION_STRING' has not been set.</exception>
    string GetConnectionString();

    /// <summary>
    /// Get connection string for connection with read-only permissions.
    /// </summary>
    /// <returns>Connection string from environment configuration.</returns>
    string GetReadOnlyConnectionString();
}
