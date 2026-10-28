// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace Mssql.McpServer.ConfigurationUtility.Model;

/// <summary>
/// A connection string to test, with the name it is reported under.
/// </summary>
public class ConnectionToTest(string name, string connectionString, bool readOnly)
{
    /// <summary>
    /// Gets the name of the connection string (e.g. "Read-only").
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the connection string. Blank if not configured.
    /// </summary>
    public string ConnectionString { get; } = connectionString;

    /// <summary>
    /// Gets a value indicating whether to connect with read-only intent.
    /// </summary>
    public bool ReadOnly { get; } = readOnly;
}
