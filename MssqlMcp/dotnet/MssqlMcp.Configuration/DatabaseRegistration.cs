// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration;

/// <summary>
/// A database the MCP server can connect to, identified by a key passed in with each tool call.
/// </summary>
public class DatabaseRegistration(string databaseKey, string? readOnlyConnectionString, string? writeConnectionString, bool isActive = true, string environmentName = DatabaseSummary.UnspecifiedEnvironmentName, string description = "")
{
    /// <summary>
    /// Gets the key that identifies the database in tool calls.
    /// </summary>
    public string DatabaseKey { get; } = databaseKey;

    /// <summary>
    /// Gets the decrypted connection string used by read-only tools, or null if none is configured.
    /// </summary>
    public string? ReadOnlyConnectionString { get; } = readOnlyConnectionString;

    /// <summary>
    /// Gets the decrypted connection string used by tools that write, or null if none is configured.
    /// </summary>
    public string? WriteConnectionString { get; } = writeConnectionString;

    /// <summary>
    /// Gets a value indicating whether the MCP server can use the database. Inactive databases are temporarily
    /// disabled: they are not listed, and tools called with their key return an error.
    /// </summary>
    public bool IsActive { get; } = isActive;

    /// <summary>
    /// Gets the environment the database belongs to (e.g. "Dev" or "Live").
    /// </summary>
    public string EnvironmentName { get; } = environmentName;

    /// <summary>
    /// Gets a description of the database for the agent (e.g. "Main 3sixty database").
    /// </summary>
    public string Description { get; } = description;
}
