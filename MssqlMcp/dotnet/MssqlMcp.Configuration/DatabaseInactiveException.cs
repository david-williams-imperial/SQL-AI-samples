// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.Configuration;

/// <summary>
/// Thrown when a tool is called with the key of a database that has been made inactive.
/// </summary>
public class DatabaseInactiveException : InvalidOperationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DatabaseInactiveException"/> class.
    /// </summary>
    /// <param name="databaseKey">Key of the inactive database.</param>
    public DatabaseInactiveException(string databaseKey)
        : base($"The database '{databaseKey}' is currently inactive, so it cannot be used. " +
               "A user can reactivate it in the MCP server configuration utility if access is needed.")
    {
        DatabaseKey = databaseKey;
    }

    /// <summary>
    /// Gets the key of the inactive database.
    /// </summary>
    public string DatabaseKey { get; }
}
