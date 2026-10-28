// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Security.Cryptography;
using System.Text;

namespace Mssql.McpServer.Configuration;

/// <summary>
/// Encrypts and decrypts connection strings with the Windows Data Protection API (DPAPI).
/// Values can only be decrypted by the Windows user account that encrypted them.
/// </summary>
public static class ConnectionStringProtector
{
    /// <summary>
    /// Additional entropy, so values encrypted by other applications for the same user cannot be substituted.
    /// </summary>
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MssqlMcp.ConnectionString");

    /// <summary>
    /// Encrypts a connection string for the current Windows user.
    /// </summary>
    /// <param name="connectionString">Connection string to encrypt.</param>
    /// <returns>Base64-encoded encrypted value.</returns>
    /// <exception cref="PlatformNotSupportedException">If not running on Windows.</exception>
    public static string Protect(string connectionString)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Connection string encryption requires Windows.");
        }

        byte[] plainBytes = Encoding.UTF8.GetBytes(connectionString);
        byte[] protectedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(protectedBytes);
    }

    /// <summary>
    /// Decrypts a connection string encrypted by <see cref="Protect"/>.
    /// </summary>
    /// <param name="protectedValue">Base64-encoded encrypted value.</param>
    /// <returns>Decrypted connection string.</returns>
    /// <exception cref="PlatformNotSupportedException">If not running on Windows.</exception>
    /// <exception cref="FormatException">If the value is not Base64 (e.g. a plain-text connection string).</exception>
    /// <exception cref="CryptographicException">If the value was encrypted by a different Windows user, or is corrupt.</exception>
    public static string Unprotect(string protectedValue)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Connection string decryption requires Windows.");
        }

        byte[] protectedBytes = Convert.FromBase64String(protectedValue);
        byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
