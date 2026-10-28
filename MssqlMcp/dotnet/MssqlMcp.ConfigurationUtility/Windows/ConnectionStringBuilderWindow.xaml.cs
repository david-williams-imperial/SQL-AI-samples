// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Windows;
using Mssql.McpServer.ConfigurationUtility.ViewModels;

namespace Mssql.McpServer.ConfigurationUtility.Windows;

/// <summary>
/// Window for building a connection string from its server, database, and authentication settings.
/// </summary>
public partial class ConnectionStringBuilderWindow : Window
{
    private readonly ConnectionStringBuilderWindowViewModel _viewModel;

    /// <summary>
    /// Initialises a new instance of the <see cref="ConnectionStringBuilderWindow"/> class.
    /// </summary>
    /// <param name="viewModel">View model.</param>
    public ConnectionStringBuilderWindow(ConnectionStringBuilderWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>
    /// Tests the connection string.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        btnTestConnection.IsEnabled = false;

        try
        {
            await _viewModel.TestConnectionAsync();
        }
        finally
        {
            btnTestConnection.IsEnabled = true;
        }
    }

    /// <summary>
    /// Closes the window, accepting the connection string, if the settings are valid.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Validate())
        {
            DialogResult = true;
        }
    }
}
