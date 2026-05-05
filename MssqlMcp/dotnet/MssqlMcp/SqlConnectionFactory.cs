// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.SqlClient;

namespace Mssql.McpServer;

/// <summary>
/// Factory for <see cref="SqlConnection"/>.
/// </summary>
public class SqlConnectionFactory : ISqlConnectionFactory
{
    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with write permissions.
    /// </summary>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    public async Task<SqlConnection> GetOpenConnectionAsync()
    {
        string connectionString = GetConnectionString();

        SqlConnection conn = new SqlConnection(connectionString);

        await conn.OpenAsync();

        return conn;
    }

    /// <summary>
    /// Gets an open <see cref="SqlConnection"/> with read-only permissions.
    /// </summary>
    /// <returns><see cref="SqlConnection"/> in the <see cref="ConnectionState.Open"/> state.</returns>
    public async Task<SqlConnection> GetOpenReadOnlyConnectionAsync()
    {
        string connectionString = GetReadOnlyConnectionString();

        SqlConnection conn = new SqlConnection(connectionString);

        await conn.OpenAsync();

        return conn;
    }

    /// <summary>
    /// Get connection string for connection with write permissions.
    /// </summary>
    /// <returns>Connection string from environment configuration.</returns>
    /// <exception cref="InvalidOperationException">If the environment variable 'CONNECTION_STRING' has not been set.</exception>
    private static string GetConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string is not set in the environment variable 'CONNECTION_STRING'." + Environment.NewLine +
                "HINT: Set the environment variable before starting the server, e.g.:" + Environment.NewLine +
                "  SET CONNECTION_STRING=Server=.;Database=test;Trusted_Connection=True;TrustServerCertificate=True");
        }

        connectionString = AdjustConnectionString(connectionString, false);

        return connectionString;
    }

    /// <summary>
    /// Get connection string for connection with read-only permissions.
    /// </summary>
    /// <returns>Connection string from environment configuration.</returns>
    private static string GetReadOnlyConnectionString()
    {
        string? connectionString = Environment.GetEnvironmentVariable("READ_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A read-only connection string is not set in the environment variable 'READ_CONNECTION_STRING'." + Environment.NewLine +
                "HINT: Set the environment variable before starting the MCP server, e.g.:" + Environment.NewLine +
                "  SET READ_CONNECTION_STRING=Server=.;Database=test;Trusted_Connection=True;TrustServerCertificate=True");
        }

        connectionString = AdjustConnectionString(connectionString, true);

        return connectionString;
    }

    /// <summary>
    /// Adds custom properties to a connection string read from the environment.
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
