// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Mssql.McpServer.ConfigurationUtility.Model;
using Mssql.McpServer.ConfigurationUtility.Services;

namespace Mssql.McpServer.ConfigurationUtility.ViewModels;

/// <summary>
/// View model for the window that adds or edits a database.
/// </summary>
public class DatabaseAddEditWindowViewModel : ViewModelBase
{
    private const string ReadOnlyConnectionName = "Read-only";

    private const string WriteConnectionName = "Write";

    private readonly SqliteDatabaseRegistry _registry;

    private readonly IConnectionTestService _connectionTestService;

    /// <summary>
    /// Initialises a new instance of the <see cref="DatabaseAddEditWindowViewModel"/> class.
    /// </summary>
    /// <param name="registry">Configuration database.</param>
    /// <param name="messageBoxService">Service used to display messages.</param>
    /// <param name="connectionTestService">Service used to test connections.</param>
    /// <param name="database">Database to add or edit.</param>
    /// <param name="environmentSuggestions">Environments to offer.</param>
    public DatabaseAddEditWindowViewModel(SqliteDatabaseRegistry registry, IMessageBoxService messageBoxService, IConnectionTestService connectionTestService, DatabaseEditModel database, IReadOnlyList<string> environmentSuggestions)
        : base(messageBoxService)
    {
        _registry = registry;
        _connectionTestService = connectionTestService;
        Database = database;
        EnvironmentSuggestions = environmentSuggestions;
    }

    /// <summary>
    /// Gets the environments offered in the environment drop-down. Others can be typed in.
    /// </summary>
    public IReadOnlyList<string> EnvironmentSuggestions { get; }

    /// <summary>
    /// Gets the database being added or edited.
    /// </summary>
    public DatabaseEditModel Database { get; }

    /// <summary>
    /// Gets the window title.
    /// </summary>
    public string WindowTitle => Database.IsAddMode ? "Add database" : $"Edit database - {Database.OriginalDatabaseKey}";

    /// <summary>
    /// Creates the view model for building one of the connection strings.
    /// A blank write connection string starts from the read-only one, as usually only the credentials differ.
    /// </summary>
    /// <param name="readOnly">Build the read-only connection string (true) or the write connection string (false)?</param>
    /// <returns>View model for the connection string builder window.</returns>
    public ConnectionStringBuilderWindowViewModel CreateBuilderViewModel(bool readOnly)
    {
        string startingConnectionString;

        if (readOnly)
        {
            startingConnectionString = Database.ReadOnlyConnectionString;
        }
        else
        {
            startingConnectionString = string.IsNullOrWhiteSpace(Database.WriteConnectionString) ? Database.ReadOnlyConnectionString : Database.WriteConnectionString;
        }

        ConnectionStringBuilderModel builder = new ConnectionStringBuilderModel(startingConnectionString);

        return new ConnectionStringBuilderWindowViewModel(MessageBoxService, _connectionTestService, builder, readOnly ? ReadOnlyConnectionName : WriteConnectionName, readOnly);
    }

    /// <summary>
    /// Sets one of the connection strings to the one built in the connection string builder window.
    /// </summary>
    /// <param name="readOnly">Set the read-only connection string (true) or the write connection string (false)?</param>
    /// <param name="connectionString">Built connection string.</param>
    public void ApplyBuiltConnectionString(bool readOnly, string connectionString)
    {
        if (readOnly)
        {
            Database.ReadOnlyConnectionString = connectionString;
        }
        else
        {
            Database.WriteConnectionString = connectionString;
        }
    }

    /// <summary>
    /// Tests both connection strings and displays the results.
    /// </summary>
    /// <returns>A task that completes when the results have been displayed.</returns>
    public async Task TestConnectionsAsync()
    {
        Database.Trim();

        await TestConnectionsAsync(_connectionTestService, [
            new ConnectionToTest(ReadOnlyConnectionName, Database.ReadOnlyConnectionString, true),
            new ConnectionToTest(WriteConnectionName, Database.WriteConnectionString, false)]);
    }

    /// <summary>
    /// Validates and saves the database, displaying any errors.
    /// </summary>
    /// <returns>True if the database was saved.</returns>
    public async Task<bool> SaveAsync()
    {
        Database.Trim();

        ShowWaitCursor();

        try
        {
            IReadOnlyList<string> existingDatabaseKeys = _registry.ConfigurationDatabaseExists
                ? await _registry.GetDatabaseKeysAsync(activeOnly: false)
                : Array.Empty<string>();

            List<string> errors = Database.GetValidationErrors(existingDatabaseKeys);

            if (errors.Count > 0)
            {
                DisplayValidationErrors(errors);

                return false;
            }

            DatabaseRegistration registration = Database.ToRegistration();

            if (Database.OriginalDatabaseKey == null)
            {
                await _registry.AddDatabaseAsync(registration);

                return true;
            }

            bool updated = await _registry.UpdateDatabaseAsync(Database.OriginalDatabaseKey, registration);

            if (!updated)
            {
                DisplayError("Unable to save", $"The database '{Database.OriginalDatabaseKey}' no longer exists. It may have been deleted by another program.");

                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            DisplayError("Unable to save", ex.Message);

            return false;
        }
        finally
        {
            HideWaitCursor();
        }
    }
}
