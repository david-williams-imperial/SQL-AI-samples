// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.SqlClient;

namespace Mssql.McpServer;

/// <summary>
/// Factory for <see cref="SqlConnection"/>.
/// </summary>
public class SqlConnectionFactory(IDatabaseRegistry databaseRegistry) : ISqlConnectionFactory
{
    private readonly IDatabaseRegistry _databaseRegistry = databaseRegistry;

    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with write permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database to connect to.</param>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    public async Task<SqlConnection> GetOpenConnectionAsync(string databaseKey)
    {
        string connectionString = await GetConnectionStringAsync(databaseKey);

        SqlConnection conn = new SqlConnection(connectionString);

        await conn.OpenAsync();

        return conn;
    }

    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with read-only permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database to connect to.</param>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    public async Task<SqlConnection> GetOpenReadOnlyConnectionAsync(string databaseKey)
    {
        string connectionString = await GetReadOnlyConnectionStringAsync(databaseKey);

        SqlConnection conn = new SqlConnection(connectionString);

        await conn.OpenAsync();

        return conn;
    }

    /// <summary>
    /// Get connection string for connection with write permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>Connection string from the database registry.</returns>
    /// <exception cref="InvalidOperationException">If the key is not configured, or has no write connection string.</exception>
    public async Task<string> GetConnectionStringAsync(string databaseKey)
    {
        DatabaseRegistration registration = await GetRegistrationAsync(databaseKey);

        if (string.IsNullOrWhiteSpace(registration.WriteConnectionString))
        {
            throw new InvalidOperationException($"Database key '{databaseKey}' is read-only (no write connection string is configured).");
        }

        return AdjustConnectionString(registration.WriteConnectionString, false);
    }

    /// <summary>
    /// Get connection string for connection with read-only permissions.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>Connection string from the database registry.</returns>
    /// <exception cref="InvalidOperationException">If the key is not configured, or has no read-only connection string.</exception>
    public async Task<string> GetReadOnlyConnectionStringAsync(string databaseKey)
    {
        DatabaseRegistration registration = await GetRegistrationAsync(databaseKey);

        if (string.IsNullOrWhiteSpace(registration.ReadOnlyConnectionString))
        {
            throw new InvalidOperationException($"Database key '{databaseKey}' has no read-only connection string configured.");
        }

        return AdjustConnectionString(registration.ReadOnlyConnectionString, true);
    }

    /// <summary>
    /// Gets a database from the registry.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>The database.</returns>
    /// <exception cref="InvalidOperationException">If the key is not configured.</exception>
    /// <exception cref="DatabaseInactiveException">If the database is inactive.</exception>
    private async Task<DatabaseRegistration> GetRegistrationAsync(string databaseKey)
    {
        if (string.IsNullOrWhiteSpace(databaseKey))
        {
            throw new InvalidOperationException("A database key is required.");
        }

        DatabaseRegistration? registration = await _databaseRegistry.GetDatabaseAsync(databaseKey);

        if (registration != null && !registration.IsActive)
        {
            throw new DatabaseInactiveException(databaseKey);
        }

        if (registration == null)
        {
            IReadOnlyList<string> keys = await _databaseRegistry.GetDatabaseKeysAsync(activeOnly: true);

            string configuredKeys = keys.Count == 0
                ? "No databases are configured."
                : $"Configured keys: {string.Join(", ", keys)}.";

            throw new InvalidOperationException($"Database key '{databaseKey}' was not found. {configuredKeys}");
        }

        return registration;
    }

    /// <summary>
    /// Adds custom properties to a connection string read from the database registry.
    /// </summary>
    /// <param name="connectionString">Connection string to adjust.</param>
    /// <param name="isReadOnly">Is this a read-only connection?</param>
    /// <returns>Adjusted connection string.</returns>
    private static string AdjustConnectionString(string connectionString, bool isReadOnly)
    {
        SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

        if (isReadOnly)
        {
            connectionStringBuilder.ApplicationName = "MssqlMcp (Read Only)";
            connectionStringBuilder.ApplicationIntent = ApplicationIntent.ReadOnly;
        }
        else
        {
            connectionStringBuilder.ApplicationName = "MssqlMcp";
        }

        connectionStringBuilder.ConnectTimeout = 30;

        return connectionStringBuilder.ToString();
    }
}
