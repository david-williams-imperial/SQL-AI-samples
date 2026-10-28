// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Windows;

namespace Mssql.McpServer.ConfigurationUtility.Services;

/// <summary>
/// Service that can display a (WPF) message box.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>
    /// Displays a message box.
    /// </summary>
    /// <param name="messageBoxText">The message box text.</param>
    /// <param name="caption">The title of the message box.</param>
    /// <param name="messageBoxButton">Button(s) to display on the message box.</param>
    /// <param name="messageBoxImage">Icon to display on the message box.</param>
    /// <returns>Message box result.</returns>
    MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton messageBoxButton, MessageBoxImage messageBoxImage);
}
