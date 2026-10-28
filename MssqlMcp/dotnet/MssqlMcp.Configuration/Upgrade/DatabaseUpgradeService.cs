// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Mssql.McpServer.Configuration.Upgrade;

/// <summary>
/// Inspects and changes the schema of a configuration database, and records which revisions have been applied.
/// </summary>
public class DatabaseUpgradeService
{
    private const string RevisionHistoryTableName = "DatabaseRevisionHistory";

    private const string CreateRevisionHistoryTableSql =
        "CREATE TABLE IF NOT EXISTS DatabaseRevisionHistory (" +
        "  DatabaseRevisionHistoryID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, " +
        "  RevisionNumber INTEGER NOT NULL, " +
        "  TimeAppliedUTC TEXT NOT NULL); " +
        "CREATE UNIQUE INDEX IF NOT EXISTS IDX_DatabaseRevisionHistory_RevisionNumber ON DatabaseRevisionHistory (RevisionNumber)";

    private const string SelectRevisionNumberSql = "SELECT COALESCE(MAX(RevisionNumber), 0) FROM DatabaseRevisionHistory";

    private const string InsertRevisionSql =
        "INSERT INTO DatabaseRevisionHistory (RevisionNumber, TimeAppliedUTC) VALUES (@RevisionNumber, @TimeAppliedUTC)";

    private const string TableExistsSql = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @TableName";

    private const string ColumnExistsSql = "SELECT COUNT(*) FROM pragma_table_info(@TableName) WHERE name = @ColumnName";

    private readonly SqliteConnection _connection;

    private readonly SqliteTransaction? _transaction;

    /// <summary>
    /// Initialises a new instance of the <see cref="DatabaseUpgradeService"/> class.
    /// </summary>
    /// <param name="connection">Open connection to the configuration database.</param>
    /// <param name="transaction">Transaction to run commands in, or null to run them outside a transaction.</param>
    public DatabaseUpgradeService(SqliteConnection connection, SqliteTransaction? transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    /// <summary>
    /// Checks whether a table exists.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <returns>True if the table exists.</returns>
    public async Task<bool> TableExistsAsync(string tableName)
    {
        await using (SqliteCommand command = CreateCommand(TableExistsSql))
        {
            command.Parameters.AddWithValue("@TableName", tableName);

            return await ExecuteCountAsync(command) > 0;
        }
    }

    /// <summary>
    /// Checks whether a column exists.
    /// </summary>
    /// <param name="tableName">Name of the table.</param>
    /// <param name="columnName">Name of the column.</param>
    /// <returns>True if the column exists; false if it, or the table, does not.</returns>
    public async Task<bool> ColumnExistsAsync(string tableName, string columnName)
    {
        await using (SqliteCommand command = CreateCommand(ColumnExistsSql))
        {
            command.Parameters.AddWithValue("@TableName", tableName);
            command.Parameters.AddWithValue("@ColumnName", columnName);

            return await ExecuteCountAsync(command) > 0;
        }
    }

    /// <summary>
    /// Runs a schema change statement.
    /// </summary>
    /// <param name="sql">SQL to run.</param>
    /// <returns>A task that completes when the statement has run.</returns>
    public async Task ExecuteAsync(string sql)
    {
        await using (SqliteCommand command = CreateCommand(sql))
        {
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Gets the most recent revision applied to the configuration database.
    /// </summary>
    /// <returns>Revision number, or 0 if no revisions have been recorded.</returns>
    public async Task<int> GetCurrentRevisionNumberAsync()
    {
        if (!await TableExistsAsync(RevisionHistoryTableName))
        {
            return 0;
        }

        await using (SqliteCommand command = CreateCommand(SelectRevisionNumberSql))
        {
            return (int)await ExecuteCountAsync(command);
        }
    }

    /// <summary>
    /// Records that a revision has been applied.
    /// </summary>
    /// <param name="revisionNumber">Revision number.</param>
    /// <returns>A task that completes when the revision has been recorded.</returns>
    public async Task SetRevisionNumberAsync(int revisionNumber)
    {
        await ExecuteAsync(CreateRevisionHistoryTableSql);

        await using (SqliteCommand command = CreateCommand(InsertRevisionSql))
        {
            command.Parameters.AddWithValue("@RevisionNumber", revisionNumber);
            command.Parameters.AddWithValue("@TimeAppliedUTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Creates a command on the connection, in the transaction if there is one.
    /// </summary>
    /// <param name="sql">SQL to run.</param>
    /// <returns>Command.</returns>
    private SqliteCommand CreateCommand(string sql)
    {
        SqliteCommand command = _connection.CreateCommand();

        command.CommandText = sql;
        command.Transaction = _transaction;

        return command;
    }

    /// <summary>
    /// Runs a command that returns a single number.
    /// </summary>
    /// <param name="command">Command to run.</param>
    /// <returns>The number.</returns>
    private static async Task<long> ExecuteCountAsync(SqliteCommand command)
    {
        object? result = await command.ExecuteScalarAsync();

        return Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
