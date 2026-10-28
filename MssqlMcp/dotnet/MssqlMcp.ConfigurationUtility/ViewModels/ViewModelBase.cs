// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text;
using System.Windows;
using System.Windows.Input;
using Mssql.McpServer.ConfigurationUtility.Model;
using Mssql.McpServer.ConfigurationUtility.Services;

namespace Mssql.McpServer.ConfigurationUtility.ViewModels;

/// <summary>
/// Base class for window view models.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    /// <param name="messageBoxService">Service used to display messages.</param>
    protected ViewModelBase(IMessageBoxService messageBoxService)
    {
        MessageBoxService = messageBoxService;
    }

    /// <summary>
    /// Gets the service used to display messages.
    /// </summary>
    protected IMessageBoxService MessageBoxService { get; }

    /// <summary>
    /// Shows the wait cursor.
    /// </summary>
    protected static void ShowWaitCursor()
    {
        Mouse.OverrideCursor = Cursors.Wait;
    }

    /// <summary>
    /// Restores the normal cursor.
    /// </summary>
    protected static void HideWaitCursor()
    {
        Mouse.OverrideCursor = null;
    }

    /// <summary>
    /// Displays a list of validation errors.
    /// </summary>
    /// <param name="validationErrors">Errors to display.</param>
    protected void DisplayValidationErrors(IReadOnlyList<string> validationErrors)
    {
        if (validationErrors.Count == 0)
        {
            return;
        }

        StringBuilder messageContent = new StringBuilder();

        messageContent.AppendLine(validationErrors.Count == 1 ? "Please correct the following error:" : "Please correct the following errors:");
        messageContent.AppendLine();

        foreach (string error in validationErrors)
        {
            messageContent.AppendLine($"• {error}");
        }

        HideWaitCursor();

        MessageBoxService.Show(messageContent.ToString(), "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Tests connection strings in parallel and displays the results together.
    /// </summary>
    /// <param name="connectionTestService">Service used to test connections.</param>
    /// <param name="connections">Connection strings to test. Blank ones are reported as not configured.</param>
    /// <returns>A task that completes when the results have been displayed.</returns>
    protected async Task TestConnectionsAsync(IConnectionTestService connectionTestService, IReadOnlyList<ConnectionToTest> connections)
    {
        ConnectionTestResult?[] results;

        ShowWaitCursor();

        try
        {
            results = await Task.WhenAll(connections.Select(async c =>
                string.IsNullOrWhiteSpace(c.ConnectionString) ? null : await connectionTestService.TestAsync(c.ConnectionString, c.ReadOnly)));
        }
        finally
        {
            HideWaitCursor();
        }

        StringBuilder messageContent = new StringBuilder();

        for (int i = 0; i < connections.Count; i++)
        {
            ConnectionTestResult? result = results[i];

            string outcome = result == null
                ? "Not configured"
                : $"{(result.Success ? "Succeeded" : "Failed")}: {result.Message}";

            messageContent.AppendLine($"{connections[i].Name} connection string:");
            messageContent.AppendLine(outcome);
            messageContent.AppendLine();
        }

        bool anyFailed = results.Any(r => r != null && !r.Success);

        MessageBoxService.Show(messageContent.ToString().TrimEnd(), "Test connection", MessageBoxButton.OK, anyFailed ? MessageBoxImage.Error : MessageBoxImage.Information);
    }

    /// <summary>
    /// Displays an error.
    /// </summary>
    /// <param name="caption">Title of the message.</param>
    /// <param name="message">Error message.</param>
    protected void DisplayError(string caption, string message)
    {
        HideWaitCursor();

        MessageBoxService.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
