// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.SqlClient;
using Mssql.McpServer.ConfigurationUtility.Model;

namespace Mssql.McpServer.ConfigurationUtility.Services;

/// <summary>
/// Tests connection strings by connecting to SQL Server.
/// </summary>
public class SqlConnectionTestService : IConnectionTestService
{
    private const string ConnectionInfoQuery =
        "SELECT @@SERVERNAME, DB_NAME(), SUSER_SNAME(), CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))";

    /// <summary>
    /// Seconds to wait for a connection before giving up.
    /// </summary>
    private const int ConnectTimeoutSeconds = 15;

    /// <summary>
    /// Opens a connection and queries the server, database, and login it connected to.
    /// </summary>
    /// <param name="connectionString">Connection string to test.</param>
    /// <param name="readOnly">Connect with read-only intent, as the MCP server does for read-only connection strings?</param>
    /// <returns>Result of the test. Never throws.</returns>
    public async Task<ConnectionTestResult> TestAsync(string connectionString, bool readOnly)
    {
        try
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString)
            {
                ApplicationName = "MssqlMcp Configuration Utility",
                ConnectTimeout = ConnectTimeoutSeconds
            };

            if (readOnly)
            {
                builder.ApplicationIntent = ApplicationIntent.ReadOnly;
            }

            await using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();

                await using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = ConnectionInfoQuery;

                    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();

                        string server = reader.IsDBNull(0) ? builder.DataSource : reader.GetString(0);
                        string database = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        string login = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        string version = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);

                        return new ConnectionTestResult(true, $"Connected to {server} (version {version}), database {database}, as {login}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult(false, ex.Message);
        }
    }
}
