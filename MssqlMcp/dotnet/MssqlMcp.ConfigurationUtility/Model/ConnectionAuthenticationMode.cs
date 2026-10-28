// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.ConfigurationUtility.Model;

/// <summary>
/// How a connection string authenticates with SQL Server.
/// </summary>
public enum ConnectionAuthenticationMode
{
    /// <summary>
    /// Windows authentication (integrated security), as the user running the MCP server.
    /// </summary>
    Windows,

    /// <summary>
    /// SQL Server authentication, with a user name and password.
    /// </summary>
    SqlServer,

    /// <summary>
    /// Another method (e.g. Microsoft Entra) set in the original connection string, left unchanged.
    /// </summary>
    Other
}
