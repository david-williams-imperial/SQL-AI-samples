// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Mssql.McpServer.ConfigurationUtility.Model;
using Mssql.McpServer.ConfigurationUtility.ViewModels;

namespace Mssql.McpServer.ConfigurationUtility.Windows;

/// <summary>
/// Main window, listing the configured databases.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    /// <summary>
    /// Initialises a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">View model.</param>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// Loads the databases when the window opens.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadRecordsAsync();
    }

    /// <summary>
    /// Lets the user choose a different configuration database.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnChangeConfigurationDatabase_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Choose configuration database",
            Filter = "SQLite databases (*.db;*.sqlite)|*.db;*.sqlite|All files (*.*)|*.*",
            CheckFileExists = false,
            FileName = Path.GetFileName(_viewModel.ConfigurationDatabasePath),
            InitialDirectory = Path.GetDirectoryName(_viewModel.ConfigurationDatabasePath)
        };

        if (dialog.ShowDialog(this) == true)
        {
            await _viewModel.ChangeConfigurationDatabaseAsync(dialog.FileName);
        }
    }

    /// <summary>
    /// Reloads the databases.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadRecordsAsync();
    }

    /// <summary>
    /// Deletes the selected database.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.DeleteSelectedItemAsync();
    }

    /// <summary>
    /// Edits the selected database.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        await EditSelectedItemAsync();
    }

    /// <summary>
    /// Tests the selected database's connection strings.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        btnTestConnection.IsEnabled = false;

        try
        {
            await _viewModel.TestSelectedItemAsync();
        }
        finally
        {
            btnTestConnection.IsEnabled = _viewModel.CanTestSelectedItem;
        }
    }

    /// <summary>
    /// Adds a database.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        await ShowAddEditWindowAsync(_viewModel.CreateAddViewModel());
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments.</param>
    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Edits the database that was double-clicked.
    /// </summary>
    /// <param name="sender">Row that was double-clicked.</param>
    /// <param name="e">Event arguments.</param>
    private async void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DataGridRow? row = sender as DataGridRow;
        DatabaseEditModel? database = row?.Item as DatabaseEditModel;

        if (database != null && database == _viewModel.SelectedItem)
        {
            await EditSelectedItemAsync();
        }
    }

    /// <summary>
    /// Opens the selected database for editing.
    /// </summary>
    /// <returns>A task that completes when the window has closed and the list has been reloaded if needed.</returns>
    private async Task EditSelectedItemAsync()
    {
        DatabaseAddEditWindowViewModel? editViewModel = _viewModel.CreateEditViewModel();

        if (editViewModel != null)
        {
            await ShowAddEditWindowAsync(editViewModel);
        }
    }

    /// <summary>
    /// Shows the add/edit window, reloading the list if the database was saved.
    /// </summary>
    /// <param name="editViewModel">View model for the add/edit window.</param>
    /// <returns>A task that completes when the window has closed and the list has been reloaded if needed.</returns>
    private async Task ShowAddEditWindowAsync(DatabaseAddEditWindowViewModel editViewModel)
    {
        DatabaseAddEditWindow window = new DatabaseAddEditWindow(editViewModel)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            await _viewModel.LoadRecordsAsync();

            _viewModel.SelectedItem = _viewModel.Databases.FirstOrDefault(d => d.DatabaseKey == editViewModel.Database.DatabaseKey);
        }
    }
}
