// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer;

/// <summary>
/// Represents the result of a database operation, including success status, error message, number of rows affected, and any returned data.
/// </summary>
public class DbOperationResult(bool success, string? error = null, int? rowsAffected = null, object? data = null)
{
    /// <summary>
    /// Gets the key of the database the operation ran against, if applicable.
    /// </summary>
    public string? DatabaseKey { get; internal set; }

    /// <summary>
    /// Gets the name of the database the operation ran against, if a connection was made.
    /// </summary>
    public string? DatabaseName { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the database operation was successful.
    /// </summary>
    public bool Success { get; } = success;

    /// <summary>
    /// Gets the error message if the operation failed; otherwise, null.
    /// </summary>
    public string? Error { get; } = error;

    /// <summary>
    /// Gets the number of rows affected by the operation, if applicable.
    /// </summary>
    public int? RowsAffected { get; } = rowsAffected;

    /// <summary>
    /// Gets any data returned by the operation, such as query results.
    /// </summary>
    public object? Data { get; } = data;
}
