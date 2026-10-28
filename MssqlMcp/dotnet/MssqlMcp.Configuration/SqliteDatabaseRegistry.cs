// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Mssql.McpServer.Configuration.Upgrade;

namespace Mssql.McpServer.Configuration;

/// <summary>
/// Reads and writes the configured databases in an SQLite file. Connection strings are stored encrypted with DPAPI.
/// The file is read on every call, so changes take effect without restarting the MCP server.
/// The first time the file is opened, any outstanding schema revisions are applied (see <see cref="DatabaseRevisionManager"/>).
/// </summary>
public class SqliteDatabaseRegistry : IDatabaseRegistry
{
    /// <summary>
    /// Environment variable that overrides the location of the configuration database.
    /// </summary>
    public const string PathEnvironmentVariable = "CONFIG_DATABASE_PATH";

    /// <summary>
    /// File name of the configuration database when <see cref="PathEnvironmentVariable"/> is not set.
    /// </summary>
    public const string DefaultFileName = "SQLServerMCP.db";

    private const string SelectAllKeysSql = "SELECT DatabaseKey FROM SQLServerDatabase ORDER BY DatabaseKey";

    private const string SelectActiveKeysSql = "SELECT DatabaseKey FROM SQLServerDatabase WHERE IsActive <> 0 ORDER BY DatabaseKey";

    private const string SelectAllSummariesSql = "SELECT DatabaseKey, IsActive, EnvironmentName, Description FROM SQLServerDatabase ORDER BY DatabaseKey";

    private const string SelectActiveSummariesSql = "SELECT DatabaseKey, IsActive, EnvironmentName, Description FROM SQLServerDatabase WHERE IsActive <> 0 ORDER BY DatabaseKey";

    private const string SelectDatabaseSql =
        "SELECT ReadOnlyConnectionString, WriteConnectionString, IsActive, EnvironmentName, Description FROM SQLServerDatabase WHERE DatabaseKey = @DatabaseKey";

    /// <summary>
    /// Adds or updates a database. A null @EnvironmentName or @Description keeps an existing database's value, or gives a new one the default.
    /// </summary>
    private const string UpsertDatabaseSql =
        "INSERT INTO SQLServerDatabase (DatabaseKey, ReadOnlyConnectionString, WriteConnectionString, EnvironmentName, Description) " +
        "VALUES (@DatabaseKey, @ReadOnlyConnectionString, @WriteConnectionString, COALESCE(@EnvironmentName, '" + DatabaseSummary.UnspecifiedEnvironmentName + "'), COALESCE(@Description, '')) " +
        "ON CONFLICT (DatabaseKey) DO UPDATE SET " +
        "  ReadOnlyConnectionString = excluded.ReadOnlyConnectionString, " +
        "  WriteConnectionString = excluded.WriteConnectionString, " +
        "  EnvironmentName = COALESCE(@EnvironmentName, SQLServerDatabase.EnvironmentName), " +
        "  Description = COALESCE(@Description, SQLServerDatabase.Description)";

    private const string InsertDatabaseSql =
        "INSERT INTO SQLServerDatabase (DatabaseKey, ReadOnlyConnectionString, WriteConnectionString, IsActive, EnvironmentName, Description) " +
        "VALUES (@DatabaseKey, @ReadOnlyConnectionString, @WriteConnectionString, @IsActive, @EnvironmentName, @Description)";

    private const string UpdateDatabaseSql =
        "UPDATE SQLServerDatabase SET " +
        "  DatabaseKey = @DatabaseKey, " +
        "  ReadOnlyConnectionString = @ReadOnlyConnectionString, " +
        "  WriteConnectionString = @WriteConnectionString, " +
        "  IsActive = @IsActive, " +
        "  EnvironmentName = @EnvironmentName, " +
        "  Description = @Description " +
        "WHERE DatabaseKey = @OriginalDatabaseKey";

    private const string DeleteDatabaseSql = "DELETE FROM SQLServerDatabase WHERE DatabaseKey = @DatabaseKey";

    /// <summary>
    /// SQLite result code for a constraint violation, e.g. a duplicate database key.
    /// </summary>
    private const int SqliteConstraintErrorCode = 19;

    /// <summary>
    /// Schema revisions, shared by all instances.
    /// </summary>
    private static readonly Lazy<DatabaseRevisionManager> RevisionManager = new Lazy<DatabaseRevisionManager>(DatabaseRevisionManager.CreateDefault);

    /// <summary>
    /// Ensures only one call upgrades the configuration database when several arrive at once.
    /// </summary>
    private readonly SemaphoreSlim _upgradeLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Has the configuration database been checked for (and brought up to) the latest schema revision?
    /// </summary>
    private bool _isUpgraded;

    /// <summary>
    /// Initialises a new instance of the <see cref="SqliteDatabaseRegistry"/> class.
    /// </summary>
    /// <param name="databasePath">Path of the SQLite configuration database.</param>
    public SqliteDatabaseRegistry(string databasePath)
    {
        DatabasePath = databasePath;
    }

    /// <summary>
    /// Gets the latest schema revision of the configuration database.
    /// </summary>
    public static int LatestRevisionNumber => RevisionManager.Value.LatestRevisionNumber;

    /// <summary>
    /// Gets the path of the SQLite configuration database.
    /// </summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration database file exists.
    /// </summary>
    public bool ConfigurationDatabaseExists => File.Exists(DatabasePath);

    /// <summary>
    /// Gets the configuration database path from the <see cref="PathEnvironmentVariable"/> environment variable,
    /// or <see cref="DefaultFileName"/> in the running application's folder. The MCP server and configuration
    /// utility therefore share a configuration database when they are deployed to the same folder.
    /// </summary>
    /// <returns>Full path of the SQLite configuration database.</returns>
    public static string GetConfiguredPath()
    {
        string? path = Environment.GetEnvironmentVariable(PathEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(path))
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        }

        return Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetDatabaseKeysAsync(bool activeOnly)
    {
        List<string> keys = new List<string>();

        await using (SqliteConnection connection = await OpenReadOnlyConnectionAsync())
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = activeOnly ? SelectActiveKeysSql : SelectAllKeysSql;

                await using (SqliteDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        keys.Add(reader.GetString(0));
                    }
                }
            }
        }

        return keys;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DatabaseSummary>> GetDatabaseSummariesAsync(bool activeOnly)
    {
        List<DatabaseSummary> summaries = new List<DatabaseSummary>();

        await using (SqliteConnection connection = await OpenReadOnlyConnectionAsync())
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = activeOnly ? SelectActiveSummariesSql : SelectAllSummariesSql;

                await using (SqliteDataReader reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        summaries.Add(new DatabaseSummary(reader.GetString(0), reader.GetInt64(1) != 0, reader.GetString(2), reader.GetString(3)));
                    }
                }
            }
        }

        return summaries;
    }

    /// <inheritdoc/>
    public async Task<DatabaseRegistration?> GetDatabaseAsync(string databaseKey)
    {
        await using (SqliteConnection connection = await OpenReadOnlyConnectionAsync())
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = SelectDatabaseSql;
                command.Parameters.AddWithValue("@DatabaseKey", databaseKey);

                await using (SqliteDataReader reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return null;
                    }

                    string? readOnlyConnectionString = Unprotect(databaseKey, reader.IsDBNull(0) ? null : reader.GetString(0));
                    string? writeConnectionString = Unprotect(databaseKey, reader.IsDBNull(1) ? null : reader.GetString(1));
                    bool isActive = reader.GetInt64(2) != 0;
                    string environmentName = reader.GetString(3);
                    string description = reader.GetString(4);

                    return new DatabaseRegistration(databaseKey, readOnlyConnectionString, writeConnectionString, isActive, environmentName, description);
                }
            }
        }
    }

    /// <summary>
    /// Adds a database, or replaces the connection strings of an existing one. Connection strings are encrypted before saving.
    /// New databases are active; existing databases keep their active state.
    /// The configuration database is created if it does not exist.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <param name="readOnlyConnectionString">Connection string used by read-only tools.</param>
    /// <param name="writeConnectionString">Connection string used by tools that write, or null to disable writing.</param>
    /// <param name="environmentName">Environment of the database (e.g. "Dev"), or null/blank to keep an existing database's
    /// environment (new databases are <see cref="DatabaseSummary.UnspecifiedEnvironmentName"/>).</param>
    /// <param name="description">Description of the database for the agent, or null/blank to keep an existing database's
    /// description (new databases have a blank description).</param>
    /// <returns>A task that completes when the database has been saved.</returns>
    public async Task SetDatabaseAsync(string databaseKey, string readOnlyConnectionString, string? writeConnectionString, string? environmentName = null, string? description = null)
    {
        await using (SqliteConnection connection = await OpenWriteConnectionAsync())
        {
            await using (SqliteCommand command = CreateSaveCommand(connection, UpsertDatabaseSql, databaseKey, readOnlyConnectionString, writeConnectionString))
            {
                command.Parameters.AddWithValue("@EnvironmentName", string.IsNullOrWhiteSpace(environmentName) ? DBNull.Value : environmentName.Trim());
                command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description.Trim());

                await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>
    /// Adds a new database. Connection strings are encrypted before saving.
    /// The configuration database is created if it does not exist.
    /// </summary>
    /// <param name="database">Database to add. A blank environment is saved as <see cref="DatabaseSummary.UnspecifiedEnvironmentName"/>.</param>
    /// <returns>A task that completes when the database has been saved.</returns>
    /// <exception cref="ArgumentException">If the database has no read-only connection string.</exception>
    /// <exception cref="InvalidOperationException">If the key is already configured.</exception>
    public async Task AddDatabaseAsync(DatabaseRegistration database)
    {
        await using (SqliteConnection connection = await OpenWriteConnectionAsync())
        {
            await using (SqliteCommand command = CreateSaveCommand(connection, InsertDatabaseSql, database))
            {
                await ExecuteCheckingForDuplicateKeyAsync(command, database.DatabaseKey);
            }
        }
    }

    /// <summary>
    /// Updates an existing database, optionally renaming its key. Connection strings are encrypted before saving.
    /// </summary>
    /// <param name="originalDatabaseKey">Current key of the database.</param>
    /// <param name="database">New details of the database, including its key (the same as <paramref name="originalDatabaseKey"/> if not renaming).
    /// A blank environment is saved as <see cref="DatabaseSummary.UnspecifiedEnvironmentName"/>.</param>
    /// <returns>True if the database was updated; false if <paramref name="originalDatabaseKey"/> was not configured.</returns>
    /// <exception cref="ArgumentException">If the database has no read-only connection string.</exception>
    /// <exception cref="InvalidOperationException">If the database is renamed to a key that is already configured.</exception>
    public async Task<bool> UpdateDatabaseAsync(string originalDatabaseKey, DatabaseRegistration database)
    {
        await using (SqliteConnection connection = await OpenWriteConnectionAsync())
        {
            await using (SqliteCommand command = CreateSaveCommand(connection, UpdateDatabaseSql, database))
            {
                command.Parameters.AddWithValue("@OriginalDatabaseKey", originalDatabaseKey);

                int rowsAffected = await ExecuteCheckingForDuplicateKeyAsync(command, database.DatabaseKey);

                return rowsAffected > 0;
            }
        }
    }

    /// <summary>
    /// Removes a database.
    /// </summary>
    /// <param name="databaseKey">Key of the database.</param>
    /// <returns>True if the database was removed; false if the key was not configured.</returns>
    public async Task<bool> RemoveDatabaseAsync(string databaseKey)
    {
        await using (SqliteConnection connection = await OpenWriteConnectionAsync())
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = DeleteDatabaseSql;
                command.Parameters.AddWithValue("@DatabaseKey", databaseKey);

                int rowsAffected = await command.ExecuteNonQueryAsync();

                return rowsAffected > 0;
            }
        }
    }

    /// <summary>
    /// Creates a command that saves all of a database's details, with the connection strings encrypted.
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="sql">SQL to run.</param>
    /// <param name="database">Database to save.</param>
    /// <returns>Command ready to run.</returns>
    /// <exception cref="ArgumentException">If the database has no read-only connection string.</exception>
    private static SqliteCommand CreateSaveCommand(SqliteConnection connection, string sql, DatabaseRegistration database)
    {
        if (string.IsNullOrWhiteSpace(database.ReadOnlyConnectionString))
        {
            throw new ArgumentException("A read-only connection string is required.", nameof(database));
        }

        string environmentName = string.IsNullOrWhiteSpace(database.EnvironmentName) ? DatabaseSummary.UnspecifiedEnvironmentName : database.EnvironmentName.Trim();

        SqliteCommand command = CreateSaveCommand(connection, sql, database.DatabaseKey, database.ReadOnlyConnectionString, database.WriteConnectionString);

        command.Parameters.AddWithValue("@IsActive", database.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@EnvironmentName", environmentName);
        command.Parameters.AddWithValue("@Description", database.Description.Trim());

        return command;
    }

    /// <summary>
    /// Creates a command that saves a database, with the key and encrypted connection strings as parameters.
    /// </summary>
    /// <param name="connection">Open connection.</param>
    /// <param name="sql">SQL to run.</param>
    /// <param name="databaseKey">Key of the database.</param>
    /// <param name="readOnlyConnectionString">Connection string used by read-only tools.</param>
    /// <param name="writeConnectionString">Connection string used by tools that write, or null/blank to disable writing.</param>
    /// <returns>Command ready to run.</returns>
    private static SqliteCommand CreateSaveCommand(SqliteConnection connection, string sql, string databaseKey, string readOnlyConnectionString, string? writeConnectionString)
    {
        string protectedReadOnlyConnectionString = ConnectionStringProtector.Protect(readOnlyConnectionString);
        string? protectedWriteConnectionString = string.IsNullOrWhiteSpace(writeConnectionString) ? null : ConnectionStringProtector.Protect(writeConnectionString);

        SqliteCommand command = connection.CreateCommand();

        command.CommandText = sql;
        command.Parameters.AddWithValue("@DatabaseKey", databaseKey);
        command.Parameters.AddWithValue("@ReadOnlyConnectionString", protectedReadOnlyConnectionString);
        command.Parameters.AddWithValue("@WriteConnectionString", protectedWriteConnectionString as object ?? DBNull.Value);

        return command;
    }

    /// <summary>
    /// Runs a command that saves a database, converting a duplicate key error into a readable exception.
    /// </summary>
    /// <param name="command">Command to run.</param>
    /// <param name="databaseKey">Key of the database being saved.</param>
    /// <returns>Number of rows affected.</returns>
    /// <exception cref="InvalidOperationException">If the key is already configured.</exception>
    private static async Task<int> ExecuteCheckingForDuplicateKeyAsync(SqliteCommand command, string databaseKey)
    {
        try
        {
            return await command.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintErrorCode)
        {
            throw new InvalidOperationException($"Database key '{databaseKey}' already exists.", ex);
        }
    }

    /// <summary>
    /// Brings the configuration database up to the latest schema revision, if this has not already been done.
    /// Only the first call per instance does any work.
    /// </summary>
    /// <returns>Revision numbers applied, in order; empty if the database was already up to date or has been checked before.</returns>
    /// <exception cref="InvalidOperationException">If the configuration database does not exist, or is from a newer version of the program.</exception>
    public async Task<IReadOnlyList<int>> UpgradeAsync()
    {
        ThrowIfNotFound();

        if (_isUpgraded)
        {
            return [];
        }

        await using (SqliteConnection connection = new SqliteConnection(BuildConnectionString(SqliteOpenMode.ReadWrite)))
        {
            await connection.OpenAsync();

            return await UpgradeAsync(connection);
        }
    }

    /// <summary>
    /// Brings the configuration database up to the latest schema revision, if this has not already been done.
    /// </summary>
    /// <param name="connection">Open read/write connection to the configuration database.</param>
    /// <returns>Revision numbers applied, in order; empty if the database was already up to date or has been checked before.</returns>
    private async Task<IReadOnlyList<int>> UpgradeAsync(SqliteConnection connection)
    {
        if (_isUpgraded)
        {
            return [];
        }

        await _upgradeLock.WaitAsync();

        try
        {
            if (_isUpgraded)
            {
                return [];
            }

            IReadOnlyList<int> appliedRevisionNumbers = await RevisionManager.Value.ApplyOutstandingUpgradesAsync(connection);

            _isUpgraded = true;

            return appliedRevisionNumbers;
        }
        finally
        {
            _upgradeLock.Release();
        }
    }

    /// <summary>
    /// Throws if the configuration database does not exist.
    /// </summary>
    /// <exception cref="InvalidOperationException">If the configuration database does not exist.</exception>
    private void ThrowIfNotFound()
    {
        if (!ConfigurationDatabaseExists)
        {
            throw new InvalidOperationException(
                $"Configuration database '{DatabasePath}' was not found. " +
                "Add a database with 'MssqlMcp set-database <key>', or set the " +
                $"{PathEnvironmentVariable} environment variable to the location of an existing configuration database.");
        }
    }

    /// <summary>
    /// Opens a read-only connection to the configuration database, upgrading its schema first if it has not been opened before.
    /// </summary>
    /// <returns>Open connection.</returns>
    /// <exception cref="InvalidOperationException">If the configuration database does not exist, or is from a newer version of the program.</exception>
    private async Task<SqliteConnection> OpenReadOnlyConnectionAsync()
    {
        await UpgradeAsync();

        SqliteConnection connection = new SqliteConnection(BuildConnectionString(SqliteOpenMode.ReadOnly));

        await connection.OpenAsync();

        return connection;
    }

    /// <summary>
    /// Opens a read/write connection to the configuration database, creating the file if needed,
    /// and upgrading its schema first if it has not been opened before.
    /// </summary>
    /// <returns>Open connection.</returns>
    /// <exception cref="InvalidOperationException">If the configuration database is from a newer version of the program.</exception>
    private async Task<SqliteConnection> OpenWriteConnectionAsync()
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(DatabasePath));

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SqliteConnection connection = new SqliteConnection(BuildConnectionString(SqliteOpenMode.ReadWriteCreate));

        await connection.OpenAsync();

        try
        {
            await UpgradeAsync(connection);
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }

        return connection;
    }

    /// <summary>
    /// Builds an SQLite connection string for the configuration database.
    /// Pooling is disabled so the file is not held open between tool calls.
    /// </summary>
    /// <param name="mode">Open mode.</param>
    /// <returns>Connection string.</returns>
    private string BuildConnectionString(SqliteOpenMode mode)
    {
        SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = mode,
            Pooling = false
        };

        return builder.ToString();
    }

    /// <summary>
    /// Decrypts a stored connection string.
    /// </summary>
    /// <param name="databaseKey">Key of the database, for error messages.</param>
    /// <param name="protectedValue">Stored value, or null.</param>
    /// <returns>Decrypted connection string, or null if no value is stored.</returns>
    /// <exception cref="InvalidOperationException">If the value cannot be decrypted.</exception>
    private static string? Unprotect(string databaseKey, string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return ConnectionStringProtector.Unprotect(protectedValue);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            throw new InvalidOperationException(
                $"The connection string for database key '{databaseKey}' could not be decrypted. " +
                "Connection strings can only be decrypted by the Windows user who saved them, and must not be entered directly into the configuration database. " +
                $"Re-save them with the configuration utility, or 'MssqlMcp set-database {databaseKey}'.",
                ex);
        }
    }
}
