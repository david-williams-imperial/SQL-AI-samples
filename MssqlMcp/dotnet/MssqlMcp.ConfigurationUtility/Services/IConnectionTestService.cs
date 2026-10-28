// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Mssql.McpServer.ConfigurationUtility.Model;

namespace Mssql.McpServer.ConfigurationUtility.Services;

/// <summary>
/// Service that tests whether a connection string can connect to SQL Server.
/// </summary>
public interface IConnectionTestService
{
    /// <summary>
    /// Opens a connection and queries the server, database, and login it connected to.
    /// </summary>
    /// <param name="connectionString">Connection string to test.</param>
    /// <param name="readOnly">Connect with read-only intent, as the MCP server does for read-only connection strings?</param>
    /// <returns>Result of the test. Never throws.</returns>
    Task<ConnectionTestResult> TestAsync(string connectionString, bool readOnly);
}
