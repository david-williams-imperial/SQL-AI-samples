// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Mssql.McpServer.ConfigurationUtility.Model;
using Mssql.McpServer.ConfigurationUtility.Services;

namespace Mssql.McpServer.ConfigurationUtility.ViewModels;

/// <summary>
/// View model for the window that builds a connection string.
/// </summary>
public class ConnectionStringBuilderWindowViewModel : ViewModelBase
{
    private readonly IConnectionTestService _connectionTestService;

    private readonly string _connectionName;

    private readonly bool _readOnly;

    /// <summary>
    /// Initialises a new instance of the <see cref="ConnectionStringBuilderWindowViewModel"/> class.
    /// </summary>
    /// <param name="messageBoxService">Service used to display messages.</param>
    /// <param name="connectionTestService">Service used to test connections.</param>
    /// <param name="builder">Connection string being built.</param>
    /// <param name="connectionName">Name of the connection string (e.g. "Read-only").</param>
    /// <param name="readOnly">Is this a read-only connection string?</param>
    public ConnectionStringBuilderWindowViewModel(IMessageBoxService messageBoxService, IConnectionTestService connectionTestService, ConnectionStringBuilderModel builder, string connectionName, bool readOnly)
        : base(messageBoxService)
    {
        _connectionTestService = connectionTestService;
        _connectionName = connectionName;
        _readOnly = readOnly;
        Builder = builder;
    }

    /// <summary>
    /// Gets the connection string being built.
    /// </summary>
    public ConnectionStringBuilderModel Builder { get; }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle => $"Build {_connectionName.ToLowerInvariant()} connection string";

    /// <summary>
    /// Validates the settings, displaying any errors.
    /// </summary>
    /// <returns>True if the settings are valid.</returns>
    public bool Validate()
    {
        List<string> errors = Builder.GetValidationErrors();

        DisplayValidationErrors(errors);

        return errors.Count == 0;
    }

    /// <summary>
    /// Tests the connection string, if valid, and displays the result.
    /// </summary>
    /// <returns>A task that completes when the result has been displayed.</returns>
    public async Task TestConnectionAsync()
    {
        if (!Validate())
        {
            return;
        }

        await TestConnectionsAsync(_connectionTestService, [new ConnectionToTest(_connectionName, Builder.ConnectionString, _readOnly)]);
    }
}
