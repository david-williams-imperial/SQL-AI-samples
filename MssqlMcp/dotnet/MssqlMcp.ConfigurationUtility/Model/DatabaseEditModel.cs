// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Data.SqlClient;

namespace Mssql.McpServer.ConfigurationUtility.Model;

/// <summary>
/// A configured database, as shown in the database list and edited in the add/edit window.
/// </summary>
public class DatabaseEditModel : ObservableObject
{
    private string? _originalDatabaseKey;

    private string _databaseKey = string.Empty;

    private string _readOnlyConnectionString = string.Empty;

    private string _writeConnectionString = string.Empty;

    private bool _isActive = true;

    private string _environmentName = string.Empty;

    private string _description = string.Empty;

    private string? _error;

    /// <summary>
    /// Gets or sets the key the database is currently saved under, or null if it has not been saved yet.
    /// </summary>
    public string? OriginalDatabaseKey
    {
        get
        {
            return _originalDatabaseKey;
        }

        set
        {
            if (SetProperty(ref _originalDatabaseKey, value))
            {
                OnPropertyChanged(nameof(IsAddMode));
            }
        }
    }

    /// <summary>
    /// Gets or sets the key that identifies the database in tool calls.
    /// </summary>
    public string DatabaseKey
    {
        get
        {
            return _databaseKey;
        }

        set
        {
            SetProperty(ref _databaseKey, value);
        }
    }

    /// <summary>
    /// Gets or sets the connection string used by read-only tools.
    /// </summary>
    public string ReadOnlyConnectionString
    {
        get
        {
            return _readOnlyConnectionString;
        }

        set
        {
            if (SetProperty(ref _readOnlyConnectionString, value))
            {
                OnPropertyChanged(nameof(ReadOnlyConnectionStringDisplay));
            }
        }
    }

    /// <summary>
    /// Gets or sets the connection string used by tools that write. Blank if the database is read-only.
    /// </summary>
    public string WriteConnectionString
    {
        get
        {
            return _writeConnectionString;
        }

        set
        {
            if (SetProperty(ref _writeConnectionString, value))
            {
                OnPropertyChanged(nameof(WriteConnectionStringDisplay));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the MCP server can use the database.
    /// Inactive databases are not listed by the MCP server, and tools called with their key return an error.
    /// </summary>
    public bool IsActive
    {
        get
        {
            return _isActive;
        }

        set
        {
            SetProperty(ref _isActive, value);
        }
    }

    /// <summary>
    /// Gets or sets the environment the database belongs to (e.g. "Dev" or "Live"). Blank for new databases, so one must be chosen.
    /// </summary>
    public string EnvironmentName
    {
        get
        {
            return _environmentName;
        }

        set
        {
            SetProperty(ref _environmentName, value);
        }
    }

    /// <summary>
    /// Gets or sets a description of the database for the agent (e.g. "Main 3sixty database").
    /// </summary>
    public string Description
    {
        get
        {
            return _description;
        }

        set
        {
            SetProperty(ref _description, value);
        }
    }

    /// <summary>
    /// Gets or sets the reason the database could not be read (e.g. it could not be decrypted), or null.
    /// </summary>
    public string? Error
    {
        get
        {
            return _error;
        }

        set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(ReadOnlyConnectionStringDisplay));
                OnPropertyChanged(nameof(WriteConnectionStringDisplay));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the database could not be read.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    /// <summary>
    /// Gets a value indicating whether this is a new database that has not been saved yet.
    /// </summary>
    public bool IsAddMode => OriginalDatabaseKey == null;

    /// <summary>
    /// Gets the read-only connection string for display in the database list, or the error if it could not be read.
    /// </summary>
    public string ReadOnlyConnectionStringDisplay => HasError ? $"Error: {Error}" : ReadOnlyConnectionString;

    /// <summary>
    /// Gets the write connection string for display in the database list.
    /// </summary>
    public string WriteConnectionStringDisplay
    {
        get
        {
            if (HasError)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(WriteConnectionString) ? "(none - read-only)" : WriteConnectionString;
        }
    }

    /// <summary>
    /// Creates a model from a configured database.
    /// </summary>
    /// <param name="registration">Configured database.</param>
    /// <returns>Model for display in the database list.</returns>
    public static DatabaseEditModel FromRegistration(DatabaseRegistration registration)
    {
        return new DatabaseEditModel
        {
            OriginalDatabaseKey = registration.DatabaseKey,
            DatabaseKey = registration.DatabaseKey,
            ReadOnlyConnectionString = registration.ReadOnlyConnectionString ?? string.Empty,
            WriteConnectionString = registration.WriteConnectionString ?? string.Empty,
            IsActive = registration.IsActive,
            EnvironmentName = registration.EnvironmentName,
            Description = registration.Description
        };
    }

    /// <summary>
    /// Creates a model for a configured database whose connection strings could not be read.
    /// </summary>
    /// <param name="summary">The database's details that could be read.</param>
    /// <param name="error">Reason the connection strings could not be read.</param>
    /// <returns>Model for display in the database list.</returns>
    public static DatabaseEditModel FromError(DatabaseSummary summary, string error)
    {
        return new DatabaseEditModel
        {
            OriginalDatabaseKey = summary.DatabaseKey,
            DatabaseKey = summary.DatabaseKey,
            IsActive = summary.IsActive,
            EnvironmentName = summary.EnvironmentName,
            Description = summary.Description,
            Error = error
        };
    }

    /// <summary>
    /// Creates the details to save to the configuration database.
    /// </summary>
    /// <returns>Database details. A blank write connection string is saved as none.</returns>
    public DatabaseRegistration ToRegistration()
    {
        string? writeConnectionString = string.IsNullOrWhiteSpace(WriteConnectionString) ? null : WriteConnectionString;

        return new DatabaseRegistration(DatabaseKey, ReadOnlyConnectionString, writeConnectionString, IsActive, EnvironmentName, Description);
    }

    /// <summary>
    /// Creates a copy for editing, so changes are not shown in the database list unless saved.
    /// Connection strings that could not be read are left blank, to be re-entered.
    /// </summary>
    /// <returns>Copy of this model.</returns>
    public DatabaseEditModel CloneForEdit()
    {
        return new DatabaseEditModel
        {
            OriginalDatabaseKey = OriginalDatabaseKey,
            DatabaseKey = DatabaseKey,
            ReadOnlyConnectionString = ReadOnlyConnectionString,
            WriteConnectionString = WriteConnectionString,
            IsActive = IsActive,
            EnvironmentName = EnvironmentName,
            Description = Description
        };
    }

    /// <summary>
    /// Removes leading and trailing whitespace from the key, environment, description, and connection strings.
    /// </summary>
    public void Trim()
    {
        DatabaseKey = DatabaseKey.Trim();
        EnvironmentName = EnvironmentName.Trim();
        Description = Description.Trim();
        ReadOnlyConnectionString = ReadOnlyConnectionString.Trim();
        WriteConnectionString = WriteConnectionString.Trim();
    }

    /// <summary>
    /// Validates the model.
    /// </summary>
    /// <param name="existingDatabaseKeys">Keys already in the configuration database.</param>
    /// <returns>Validation errors; empty if the model is valid.</returns>
    public List<string> GetValidationErrors(IEnumerable<string> existingDatabaseKeys)
    {
        List<string> errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DatabaseKey))
        {
            errors.Add("A database key is required");
        }
        else
        {
            string? duplicateKey = existingDatabaseKeys
                .Where(k => !string.Equals(k, OriginalDatabaseKey, StringComparison.Ordinal))
                .FirstOrDefault(k => string.Equals(k, DatabaseKey, StringComparison.OrdinalIgnoreCase));

            if (duplicateKey != null)
            {
                errors.Add($"There is already a database with the key '{duplicateKey}'");
            }
        }

        if (string.IsNullOrWhiteSpace(EnvironmentName))
        {
            errors.Add("An environment is required");
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            errors.Add("A description is required");
        }

        if (string.IsNullOrWhiteSpace(ReadOnlyConnectionString))
        {
            errors.Add("A read-only connection string is required");
        }
        else
        {
            AddConnectionStringErrors(errors, "read-only", ReadOnlyConnectionString);
        }

        if (!string.IsNullOrWhiteSpace(WriteConnectionString))
        {
            AddConnectionStringErrors(errors, "write", WriteConnectionString);
        }

        return errors;
    }

    /// <summary>
    /// Checks that a connection string can be parsed by SQL Server's client library.
    /// </summary>
    /// <param name="errors">List to add errors to.</param>
    /// <param name="name">Name of the connection string, for the error message.</param>
    /// <param name="connectionString">Connection string to check.</param>
    private static void AddConnectionStringErrors(List<string> errors, string name, string connectionString)
    {
        try
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                errors.Add($"The {name} connection string must specify a server");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            errors.Add($"The {name} connection string is invalid: {ex.Message}");
        }
    }
}
