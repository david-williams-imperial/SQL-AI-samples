// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace Mssql.McpServer.ConfigurationUtility.Model;

/// <summary>
/// Builds a connection string from its server, database, and authentication settings.
/// Any other settings in the original connection string (e.g. Encrypt) are kept.
/// </summary>
public class ConnectionStringBuilderModel : ObservableObject
{
    private const string DataSourceKeyword = "Data Source";

    private const string InitialCatalogKeyword = "Initial Catalog";

    private const string TrustServerCertificateKeyword = "Trust Server Certificate";

    /// <summary>
    /// Keywords that hold credentials, which are replaced when the authentication mode is Windows or SQL Server.
    /// </summary>
    private static readonly string[] CredentialKeywords = ["Integrated Security", "User ID", "Password", "Authentication"];

    /// <summary>
    /// Original connection string, providing the settings this model does not edit.
    /// </summary>
    private readonly string _originalConnectionString;

    private string _server;

    private string _database;

    private ConnectionAuthenticationMode _authenticationMode;

    private string _userName;

    private string _password;

    private bool _trustServerCertificate;

    /// <summary>
    /// Initialises a new instance of the <see cref="ConnectionStringBuilderModel"/> class from an existing connection string.
    /// </summary>
    /// <param name="connectionString">Connection string to start from. Blank or invalid connection strings start from scratch.</param>
    public ConnectionStringBuilderModel(string? connectionString)
    {
        SqlConnectionStringBuilder builder = Parse(connectionString);

        _originalConnectionString = builder.ConnectionString;
        _server = builder.DataSource;
        _database = builder.InitialCatalog;
        _userName = builder.UserID;
        _password = builder.Password;
        _trustServerCertificate = builder.TrustServerCertificate;

        if (builder.IntegratedSecurity)
        {
            _authenticationMode = ConnectionAuthenticationMode.Windows;
        }
        else if (builder.Authentication != SqlAuthenticationMethod.NotSpecified && builder.Authentication != SqlAuthenticationMethod.SqlPassword)
        {
            _authenticationMode = ConnectionAuthenticationMode.Other;
            OtherAuthenticationMethod = builder.Authentication.ToString();
        }
        else if (!string.IsNullOrWhiteSpace(builder.UserID))
        {
            _authenticationMode = ConnectionAuthenticationMode.SqlServer;
        }
        else
        {
            _authenticationMode = ConnectionAuthenticationMode.Windows;
        }
    }

    /// <summary>
    /// Gets or sets the server name (e.g. "." or "tcp:myserver.database.windows.net,1433").
    /// </summary>
    public string Server
    {
        get
        {
            return _server;
        }

        set
        {
            SetConnectionProperty(ref _server, value);
        }
    }

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    public string Database
    {
        get
        {
            return _database;
        }

        set
        {
            SetConnectionProperty(ref _database, value);
        }
    }

    /// <summary>
    /// Gets or sets how the connection authenticates.
    /// </summary>
    public ConnectionAuthenticationMode AuthenticationMode
    {
        get
        {
            return _authenticationMode;
        }

        set
        {
            if (SetConnectionProperty(ref _authenticationMode, value))
            {
                OnPropertyChanged(nameof(IsWindowsAuthentication));
                OnPropertyChanged(nameof(IsSqlServerAuthentication));
                OnPropertyChanged(nameof(IsOtherAuthentication));
                OnPropertyChanged(nameof(IsCredentialsEnabled));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether Windows authentication is used. For binding to a radio button.
    /// </summary>
    public bool IsWindowsAuthentication
    {
        get
        {
            return AuthenticationMode == ConnectionAuthenticationMode.Windows;
        }

        set
        {
            if (value)
            {
                AuthenticationMode = ConnectionAuthenticationMode.Windows;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether SQL Server authentication is used. For binding to a radio button.
    /// </summary>
    public bool IsSqlServerAuthentication
    {
        get
        {
            return AuthenticationMode == ConnectionAuthenticationMode.SqlServer;
        }

        set
        {
            if (value)
            {
                AuthenticationMode = ConnectionAuthenticationMode.SqlServer;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the original connection string's authentication method is kept. For binding to a radio button.
    /// </summary>
    public bool IsOtherAuthentication
    {
        get
        {
            return AuthenticationMode == ConnectionAuthenticationMode.Other;
        }

        set
        {
            if (value && HasOtherAuthentication)
            {
                AuthenticationMode = ConnectionAuthenticationMode.Other;
            }
        }
    }

    /// <summary>
    /// Gets the authentication method of the original connection string, if it is neither Windows nor SQL Server authentication.
    /// </summary>
    public string? OtherAuthenticationMethod { get; }

    /// <summary>
    /// Gets a value indicating whether the original connection string used another authentication method.
    /// </summary>
    public bool HasOtherAuthentication => OtherAuthenticationMethod != null;

    /// <summary>
    /// Gets a value indicating whether the user name and password are used.
    /// </summary>
    public bool IsCredentialsEnabled => AuthenticationMode == ConnectionAuthenticationMode.SqlServer;

    /// <summary>
    /// Gets or sets the SQL Server login name.
    /// </summary>
    public string UserName
    {
        get
        {
            return _userName;
        }

        set
        {
            SetConnectionProperty(ref _userName, value);
        }
    }

    /// <summary>
    /// Gets or sets the SQL Server login password.
    /// </summary>
    public string Password
    {
        get
        {
            return _password;
        }

        set
        {
            SetConnectionProperty(ref _password, value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to trust the server's certificate without validating it
    /// (needed for servers with self-signed certificates, e.g. most development servers).
    /// </summary>
    public bool TrustServerCertificate
    {
        get
        {
            return _trustServerCertificate;
        }

        set
        {
            SetConnectionProperty(ref _trustServerCertificate, value);
        }
    }

    /// <summary>
    /// Gets the connection string built from the current settings.
    /// </summary>
    public string ConnectionString
    {
        get
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(_originalConnectionString);

            SetOrRemove(builder, DataSourceKeyword, Server.Trim());
            SetOrRemove(builder, InitialCatalogKeyword, Database.Trim());

            if (AuthenticationMode != ConnectionAuthenticationMode.Other)
            {
                foreach (string keyword in CredentialKeywords)
                {
                    builder.Remove(keyword);
                }

                if (AuthenticationMode == ConnectionAuthenticationMode.Windows)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    builder.UserID = UserName.Trim();
                    builder.Password = Password;
                }
            }

            if (TrustServerCertificate)
            {
                builder.TrustServerCertificate = true;
            }
            else
            {
                builder.Remove(TrustServerCertificateKeyword);
            }

            return builder.ConnectionString;
        }
    }

    /// <summary>
    /// Validates the settings.
    /// </summary>
    /// <returns>Validation errors; empty if the settings are valid.</returns>
    public List<string> GetValidationErrors()
    {
        List<string> errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Server))
        {
            errors.Add("A server name is required");
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            errors.Add("A database name is required");
        }

        if (IsSqlServerAuthentication && string.IsNullOrWhiteSpace(UserName))
        {
            errors.Add("A user name is required for SQL Server authentication");
        }

        return errors;
    }

    /// <summary>
    /// Parses a connection string, returning an empty builder if it is blank or invalid.
    /// </summary>
    /// <param name="connectionString">Connection string to parse.</param>
    /// <returns>Parsed connection string.</returns>
    private static SqlConnectionStringBuilder Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new SqlConnectionStringBuilder();
        }

        try
        {
            return new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            return new SqlConnectionStringBuilder();
        }
    }

    /// <summary>
    /// Sets a keyword, or removes it if the value is blank.
    /// </summary>
    /// <param name="builder">Connection string builder.</param>
    /// <param name="keyword">Keyword to set.</param>
    /// <param name="value">Value to set.</param>
    private static void SetOrRemove(SqlConnectionStringBuilder builder, string keyword, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            builder.Remove(keyword);
        }
        else
        {
            builder[keyword] = value;
        }
    }

    /// <summary>
    /// Sets a backing field and, if it changed, notifies that the connection string has changed too.
    /// </summary>
    /// <typeparam name="T">Type of the property.</typeparam>
    /// <param name="field">Backing field.</param>
    /// <param name="value">New value.</param>
    /// <param name="propertyName">Name of the property.</param>
    /// <returns>True if the value changed.</returns>
    private bool SetConnectionProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        OnPropertyChanged(nameof(ConnectionString));

        return true;
    }
}
