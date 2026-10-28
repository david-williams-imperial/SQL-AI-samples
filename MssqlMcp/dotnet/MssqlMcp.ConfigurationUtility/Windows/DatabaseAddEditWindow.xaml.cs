// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Windows;
using Mssql.McpServer.ConfigurationUtility.ViewModels;

namespace Mssql.McpServer.ConfigurationUtility.Windows;

/// <summary>
/// Window for adding or editing a database.
/// </summary>
public partial class DatabaseAddEditWindow : Window
{
    private readonly DatabaseAddEditWindowViewModel _viewModel;

    /// <summary>
    /// Initialises a new instance of the <see cref="DatabaseAddEditWindow"/> class.
    /// </summary>
    /// <param name="viewModel">View model.</param>
    public DatabaseAddEditWindow(DatabaseAddEditWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    /// <summary>
    /// Builds the read-only connection string.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private void BtnBuildReadOnlyConnectionString_Click(object sender, RoutedEventArgs e)
    {
        ShowBuilderWindow(true);
    }

    /// <summary>
    /// Builds the write connection string.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private void BtnBuildWriteConnectionString_Click(object sender, RoutedEventArgs e)
    {
        ShowBuilderWindow(false);
    }

    /// <summary>
    /// Tests both connection strings.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnTestConnections_Click(object sender, RoutedEventArgs e)
    {
        btnTestConnections.IsEnabled = false;

        try
        {
            await _viewModel.TestConnectionsAsync();
        }
        finally
        {
            btnTestConnections.IsEnabled = true;
        }
    }

    /// <summary>
    /// Shows the connection string builder window, applying the connection string if accepted.
    /// </summary>
    /// <param name="readOnly">Build the read-only connection string (true) or the write connection string (false)?</param>
    private void ShowBuilderWindow(bool readOnly)
    {
        ConnectionStringBuilderWindowViewModel builderViewModel = _viewModel.CreateBuilderViewModel(readOnly);

        ConnectionStringBuilderWindow window = new ConnectionStringBuilderWindow(builderViewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _viewModel.ApplyBuiltConnectionString(readOnly, builderViewModel.Builder.ConnectionString);
        }
    }

    /// <summary>
    /// Saves the database, closing the window if successful.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        btnSave.IsEnabled = false;

        try
        {
            if (await _viewModel.SaveAsync())
            {
                DialogResult = true;
            }
        }
        finally
        {
            btnSave.IsEnabled = true;
        }
    }
}
