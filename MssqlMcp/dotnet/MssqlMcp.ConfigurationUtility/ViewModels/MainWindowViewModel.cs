// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Collections.ObjectModel;
using System.Windows;
using Mssql.McpServer.ConfigurationUtility.Model;
using Mssql.McpServer.ConfigurationUtility.Services;

namespace Mssql.McpServer.ConfigurationUtility.ViewModels;

/// <summary>
/// View model for the main window, which lists the configured databases.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    /// <summary>
    /// Environments always offered when adding or editing a database.
    /// </summary>
    private static readonly string[] CommonEnvironmentNames = ["Dev", "Test", "UAT", "Live"];

    private readonly IConnectionTestService _connectionTestService;

    private SqliteDatabaseRegistry _registry;

    private DatabaseEditModel? _selectedItem;

    /// <summary>
    /// Initialises a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="registry">Configuration database.</param>
    /// <param name="messageBoxService">Service used to display messages.</param>
    /// <param name="connectionTestService">Service used to test connections.</param>
    public MainWindowViewModel(SqliteDatabaseRegistry registry, IMessageBoxService messageBoxService, IConnectionTestService connectionTestService)
        : base(messageBoxService)
    {
        _registry = registry;
        _connectionTestService = connectionTestService;
    }

    /// <summary>
    /// Gets the configured databases.
    /// </summary>
    public ObservableCollection<DatabaseEditModel> Databases { get; } = new ObservableCollection<DatabaseEditModel>();

    /// <summary>
    /// Gets the environments offered when adding or editing a database: common ones, plus any already in use.
    /// </summary>
    public IReadOnlyList<string> EnvironmentSuggestions
    {
        get
        {
            return CommonEnvironmentNames
                .Concat(Databases.Select(d => d.EnvironmentName))
                .Where(e => !string.IsNullOrWhiteSpace(e) && e != DatabaseSummary.UnspecifiedEnvironmentName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    /// <summary>
    /// Gets the path of the configuration database.
    /// </summary>
    public string ConfigurationDatabasePath => _registry.DatabasePath;

    /// <summary>
    /// Gets or sets the selected database.
    /// </summary>
    public DatabaseEditModel? SelectedItem
    {
        get
        {
            return _selectedItem;
        }

        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(IsItemSelected));
                OnPropertyChanged(nameof(CanTestSelectedItem));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a database is selected.
    /// </summary>
    public bool IsItemSelected => SelectedItem != null;

    /// <summary>
    /// Gets a value indicating whether the selected database's connections can be tested (i.e. they could be decrypted).
    /// </summary>
    public bool CanTestSelectedItem => SelectedItem != null && !SelectedItem.HasError;

    /// <summary>
    /// Loads all databases (active and inactive) from the configuration database, keeping the current selection if possible.
    /// Databases that cannot be decrypted are listed with an error.
    /// </summary>
    /// <returns>A task that completes when the databases have been loaded.</returns>
    public async Task LoadRecordsAsync()
    {
        string? selectedDatabaseKey = SelectedItem?.DatabaseKey;

        ShowWaitCursor();

        try
        {
            Databases.Clear();

            if (_registry.ConfigurationDatabaseExists)
            {
                IReadOnlyList<DatabaseSummary> summaries = await _registry.GetDatabaseSummariesAsync(activeOnly: false);

                foreach (DatabaseSummary summary in summaries)
                {
                    try
                    {
                        DatabaseRegistration? registration = await _registry.GetDatabaseAsync(summary.DatabaseKey);

                        if (registration != null)
                        {
                            Databases.Add(DatabaseEditModel.FromRegistration(registration));
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        Databases.Add(DatabaseEditModel.FromError(summary, ex.Message));
                    }
                }
            }

            SelectedItem = Databases.FirstOrDefault(d => d.DatabaseKey == selectedDatabaseKey);
        }
        catch (Exception ex)
        {
            DisplayError("Unable to load databases", ex.Message);
        }
        finally
        {
            HideWaitCursor();
        }
    }

    /// <summary>
    /// Switches to a different configuration database. The file is created when the first database is added.
    /// </summary>
    /// <param name="databasePath">Path of the configuration database.</param>
    /// <returns>A task that completes when the databases have been loaded.</returns>
    public async Task ChangeConfigurationDatabaseAsync(string databasePath)
    {
        _registry = new SqliteDatabaseRegistry(databasePath);
        SelectedItem = null;

        OnPropertyChanged(nameof(ConfigurationDatabasePath));

        await LoadRecordsAsync();
    }

    /// <summary>
    /// Deletes the selected database, after confirmation.
    /// </summary>
    /// <returns>A task that completes when the database has been deleted and the list reloaded.</returns>
    public async Task DeleteSelectedItemAsync()
    {
        if (SelectedItem?.OriginalDatabaseKey == null)
        {
            return;
        }

        string databaseKey = SelectedItem.OriginalDatabaseKey;

        MessageBoxResult confirmDelete = MessageBoxService.Show($"Are you sure you want to delete the database '{databaseKey}'?", "Are you sure?", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmDelete != MessageBoxResult.Yes)
        {
            return;
        }

        ShowWaitCursor();

        try
        {
            await _registry.RemoveDatabaseAsync(databaseKey);
        }
        catch (Exception ex)
        {
            DisplayError("Unable to delete", ex.Message);
        }
        finally
        {
            HideWaitCursor();
        }

        await LoadRecordsAsync();
    }

    /// <summary>
    /// Tests the selected database's connection strings and displays the results.
    /// </summary>
    /// <returns>A task that completes when the results have been displayed.</returns>
    public async Task TestSelectedItemAsync()
    {
        if (SelectedItem == null || SelectedItem.HasError)
        {
            return;
        }

        await TestConnectionsAsync(_connectionTestService, [
            new ConnectionToTest("Read-only", SelectedItem.ReadOnlyConnectionString, true),
            new ConnectionToTest("Write", SelectedItem.WriteConnectionString, false)]);
    }

    /// <summary>
    /// Creates the view model for adding a new database.
    /// </summary>
    /// <returns>View model for the add/edit window.</returns>
    public DatabaseAddEditWindowViewModel CreateAddViewModel()
    {
        return new DatabaseAddEditWindowViewModel(_registry, MessageBoxService, _connectionTestService, new DatabaseEditModel(), EnvironmentSuggestions);
    }

    /// <summary>
    /// Creates the view model for editing the selected database.
    /// </summary>
    /// <returns>View model for the add/edit window, or null if no database is selected.</returns>
    public DatabaseAddEditWindowViewModel? CreateEditViewModel()
    {
        if (SelectedItem == null)
        {
            return null;
        }

        return new DatabaseAddEditWindowViewModel(_registry, MessageBoxService, _connectionTestService, SelectedItem.CloneForEdit(), EnvironmentSuggestions);
    }
}
