// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    private const string SqlServerInfoQuery =
        "SELECT @@SERVERNAME AS server_name, " +
        "  CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS product_version, " +
        "  CAST(SERVERPROPERTY('ProductLevel') AS nvarchar(128)) AS product_level, " +
        "  CAST(SERVERPROPERTY('ProductUpdateLevel') AS nvarchar(128)) AS product_update_level, " +
        "  CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS edition, " +
        "  CAST(SERVERPROPERTY('EngineEdition') AS int) AS engine_edition, " +
        "  CAST(SERVERPROPERTY('Collation') AS nvarchar(128)) AS server_collation, " +
        "  DB_NAME() AS database_name, " +
        "  d.collation_name AS database_collation, " +
        "  d.compatibility_level, " +
        "  d.recovery_model_desc AS recovery_model, " +
        "  CAST(DATABASEPROPERTYEX(DB_NAME(), 'Updateability') AS nvarchar(128)) AS database_updateability, " +
        "  SUSER_SNAME() AS login_name, " +
        "  sp.default_language_name AS login_default_language, " +
        "  @@LANGUAGE AS session_language, " +
        "  s.date_format AS session_date_format, " +
        "  USER_NAME() AS database_user, " +
        "  CAST(IS_SRVROLEMEMBER('sysadmin') AS bit) AS is_sysadmin, " +
        "  CAST(IS_ROLEMEMBER('db_owner') AS bit) AS is_db_owner " +
        "FROM sys.databases d " +
        "LEFT JOIN sys.server_principals sp ON sp.sid = SUSER_SID() " +
        "LEFT JOIN sys.dm_exec_sessions s ON s.session_id = @@SPID " +
        "WHERE d.database_id = DB_ID()";

    /// <summary>
    /// Returns server, authentication, version, and database details for each configured connection string.
    /// </summary>
    /// <returns>Result containing one entry per connection string.</returns>
    [McpServerTool(
        Title = "Get SQL Server Info",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Returns the server, authentication, SQL Server version, and database details for each of the MCP server's connection strings (read-only and write).")]
    public async Task<DbOperationResult> GetSqlServerInfo()
    {
        Task<Dictionary<string, object?>> readOnlyTask = GetConnectionInfoAsync("read_only", "READ_CONNECTION_STRING", _connectionFactory.GetReadOnlyConnectionString);
        Task<Dictionary<string, object?>> writeTask = GetConnectionInfoAsync("write", "CONNECTION_STRING", _connectionFactory.GetConnectionString);

        Dictionary<string, object?>[] connections = await Task.WhenAll(readOnlyTask, writeTask);

        bool anyConnected = connections.Any(c => c["error"] == null);

        if (!anyConnected)
        {
            return new DbOperationResult(success: false, error: "Unable to connect using any connection string.", data: connections);
        }

        return new DbOperationResult(success: true, data: connections);
    }

    /// <summary>
    /// Describes a single connection string and queries the server it points to.
    /// </summary>
    /// <param name="name">Friendly name for the connection.</param>
    /// <param name="environmentVariable">Environment variable the connection string is read from.</param>
    /// <param name="getConnectionString">Function returning the connection string.</param>
    /// <returns>Connection details, including an error message if the server could not be queried.</returns>
    private async Task<Dictionary<string, object?>> GetConnectionInfoAsync(string name, string environmentVariable, Func<string> getConnectionString)
    {
        Dictionary<string, object?> result = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["environment_variable"] = environmentVariable,
            ["error"] = null
        };

        string connectionString;

        try
        {
            connectionString = getConnectionString();

            result["connection_string"] = DescribeConnectionString(connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSqlServerInfo failed to read {EnvironmentVariable}: {Message}", environmentVariable, ex.Message);

            result["error"] = ex.Message;

            return result;
        }

        try
        {
            await using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                await using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = SqlServerInfoQuery;

                    await using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow))
                    {
                        if (await reader.ReadAsync())
                        {
                            string? productVersion = GetValueOrNull(reader, "product_version") as string;
                            int? engineEdition = GetValueOrNull(reader, "engine_edition") as int?;

                            result["server"] = new
                            {
                                server_name = GetValueOrNull(reader, "server_name"),
                                product_name = GetSqlServerProductName(productVersion, engineEdition),
                                product_version = productVersion,
                                product_level = GetValueOrNull(reader, "product_level"),
                                product_update_level = GetValueOrNull(reader, "product_update_level"),
                                edition = GetValueOrNull(reader, "edition"),
                                engine_edition = engineEdition,
                                server_collation = GetValueOrNull(reader, "server_collation"),
                                login_name = GetValueOrNull(reader, "login_name"),
                                login_default_language = GetValueOrNull(reader, "login_default_language"),
                                session_language = GetValueOrNull(reader, "session_language"),
                                session_date_format = GetValueOrNull(reader, "session_date_format"),
                                is_sysadmin = GetValueOrNull(reader, "is_sysadmin")
                            };

                            result["database"] = new
                            {
                                database_name = GetValueOrNull(reader, "database_name"),
                                database_user = GetValueOrNull(reader, "database_user"),
                                is_db_owner = GetValueOrNull(reader, "is_db_owner"),
                                collation = GetValueOrNull(reader, "database_collation"),
                                compatibility_level = GetValueOrNull(reader, "compatibility_level"),
                                recovery_model = GetValueOrNull(reader, "recovery_model"),
                                updateability = GetValueOrNull(reader, "database_updateability")
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSqlServerInfo failed for {EnvironmentVariable}: {Message}", environmentVariable, ex.Message);

            result["error"] = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Extracts the non-sensitive settings from a connection string. Passwords are never included.
    /// </summary>
    /// <param name="connectionString">Connection string to describe.</param>
    /// <returns>Anonymous object describing the connection string.</returns>
    private static object DescribeConnectionString(string connectionString)
    {
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

        string authentication;

        if (builder.IntegratedSecurity)
        {
            authentication = "Windows authentication";
        }
        else if (builder.Authentication != SqlAuthenticationMethod.NotSpecified)
        {
            authentication = builder.Authentication.ToString();
        }
        else
        {
            authentication = "SQL Server authentication";
        }

        return new
        {
            server = builder.DataSource,
            database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? null : builder.InitialCatalog,
            authentication,
            user_id = string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID,
            application_intent = builder.ApplicationIntent.ToString(),
            encrypt = builder.Encrypt.ToString(),
            trust_server_certificate = builder.TrustServerCertificate
        };
    }

    /// <summary>
    /// Gets the marketing name of a SQL Server version (e.g. "SQL Server 2022").
    /// </summary>
    /// <param name="productVersion">Value of SERVERPROPERTY('ProductVersion').</param>
    /// <param name="engineEdition">Value of SERVERPROPERTY('EngineEdition').</param>
    /// <returns>Product name, or null if it cannot be determined.</returns>
    private static string? GetSqlServerProductName(string? productVersion, int? engineEdition)
    {
        // Azure SQL Database, Synapse, Managed Instance, SQL Edge, Synapse serverless, and Fabric
        if (engineEdition is 5 or 6 or 8 or 9 or 11 or 12)
        {
            return "Azure SQL";
        }

        if (!Version.TryParse(productVersion, out Version? version))
        {
            return null;
        }

        return (version.Major, version.Minor) switch
        {
            (10, 50) => "SQL Server 2008 R2",
            (10, _) => "SQL Server 2008",
            (11, _) => "SQL Server 2012",
            (12, _) => "SQL Server 2014",
            (13, _) => "SQL Server 2016",
            (14, _) => "SQL Server 2017",
            (15, _) => "SQL Server 2019",
            (16, _) => "SQL Server 2022",
            (17, _) => "SQL Server 2025",
            _ => null
        };
    }

    /// <summary>
    /// Gets a column value from a reader, converting <see cref="DBNull"/> to null.
    /// </summary>
    /// <param name="reader">Reader positioned on a row.</param>
    /// <param name="columnName">Name of the column.</param>
    /// <returns>Column value, or null.</returns>
    private static object? GetValueOrNull(SqlDataReader reader, string columnName)
    {
        object value = reader[columnName];

        return value is DBNull ? null : value;
    }
}
