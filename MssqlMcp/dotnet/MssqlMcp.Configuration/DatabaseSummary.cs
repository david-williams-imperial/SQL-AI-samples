// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration;

/// <summary>
/// The details of a configured database that can be read without decrypting its connection strings.
/// </summary>
public class DatabaseSummary(string databaseKey, bool isActive, string environmentName, string description)
{
    /// <summary>
    /// Environment name of databases whose environment has not been set.
    /// </summary>
    public const string UnspecifiedEnvironmentName = "Unspecified";

    /// <summary>
    /// Gets the key that identifies the database in tool calls.
    /// </summary>
    public string DatabaseKey { get; } = databaseKey;

    /// <summary>
    /// Gets a value indicating whether the MCP server can use the database.
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
