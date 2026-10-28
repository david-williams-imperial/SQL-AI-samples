// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Windows;

namespace Mssql.McpServer.ConfigurationUtility.Services;

/// <summary>
/// Service that can display a (WPF) message box.
/// </summary>
public class MessageBoxService : IMessageBoxService
{
    /// <summary>
    /// Displays a message box, owned by the active window if there is one.
    /// </summary>
    /// <param name="messageBoxText">The message box text.</param>
    /// <param name="caption">The title of the message box.</param>
    /// <param name="messageBoxButton">Button(s) to display on the message box.</param>
    /// <param name="messageBoxImage">Icon to display on the message box.</param>
    /// <returns>Message box result.</returns>
    public MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton messageBoxButton, MessageBoxImage messageBoxImage)
    {
        Window? owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

        if (owner == null)
        {
            return MessageBox.Show(messageBoxText, caption, messageBoxButton, messageBoxImage);
        }

        return MessageBox.Show(owner, messageBoxText, caption, messageBoxButton, messageBoxImage);
    }
}
