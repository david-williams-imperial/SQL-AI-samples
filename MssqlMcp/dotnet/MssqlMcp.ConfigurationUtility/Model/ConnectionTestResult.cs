// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.ConfigurationUtility.Model;

/// <summary>
/// Result of testing a connection string.
/// </summary>
public class ConnectionTestResult(bool success, string message)
{
    /// <summary>
    /// Gets a value indicating whether the connection succeeded.
    /// </summary>
    public bool Success { get; } = success;

    /// <summary>
    /// Gets a description of what was connected to, or the reason the connection failed.
    /// </summary>
    public string Message { get; } = message;
}
