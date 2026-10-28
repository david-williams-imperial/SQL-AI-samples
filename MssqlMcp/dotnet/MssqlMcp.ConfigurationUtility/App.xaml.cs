// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.IO;
using System.Windows;
using Mssql.McpServer.ConfigurationUtility.Services;
using Mssql.McpServer.ConfigurationUtility.ViewModels;
using Mssql.McpServer.ConfigurationUtility.Windows;

namespace Mssql.McpServer.ConfigurationUtility;

/// <summary>
/// Configuration utility for the MSSQL MCP server.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Opens the main window on the configuration database given on the command line,
    /// or the one the MCP server uses by default.
    /// </summary>
    /// <param name="sender">Event sender.</param>
    /// <param name="e">Event arguments, including command-line arguments.</param>
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        string databasePath = e.Args.Length > 0 && !string.IsNullOrWhiteSpace(e.Args[0])
            ? e.Args[0].Trim()
            : SqliteDatabaseRegistry.GetConfiguredPath();

        SqliteDatabaseRegistry registry = new SqliteDatabaseRegistry(Path.GetFullPath(databasePath));
        MainWindowViewModel viewModel = new MainWindowViewModel(registry, new MessageBoxService(), new SqlConnectionTestService());

        MainWindow mainWindow = new MainWindow(viewModel);

        mainWindow.Show();
    }
}
