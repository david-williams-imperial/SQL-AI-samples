// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Security.Cryptography;
using Mssql.McpServer;

namespace MssqlMcp.Tests
{
    /// <summary>
    /// Tests for <see cref="ConnectionStringProtector"/>.
    /// </summary>
    public sealed class ConnectionStringProtectorTests
    {
        private const string ConnectionString = "Server=.;Database=test;User ID=sa;Password=Secret123!;TrustServerCertificate=True";

        [Fact]
        public void Unprotect_ReturnsOriginal_WhenProtected()
        {
            string protectedValue = ConnectionStringProtector.Protect(ConnectionString);

            Assert.Equal(ConnectionString, ConnectionStringProtector.Unprotect(protectedValue));
        }

        [Fact]
        public void Protect_DoesNotContainPlainText()
        {
            string protectedValue = ConnectionStringProtector.Protect(ConnectionString);

            string decodedValue = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));

            Assert.DoesNotContain("Secret123!", decodedValue, StringComparison.Ordinal);
        }

        [Fact]
        public void Unprotect_Throws_WhenValueIsPlainText()
        {
            Assert.Throws<FormatException>(() => ConnectionStringProtector.Unprotect(ConnectionString));
        }

        [Fact]
        public void Unprotect_Throws_WhenValueIsCorrupt()
        {
            byte[] protectedBytes = Convert.FromBase64String(ConnectionStringProtector.Protect(ConnectionString));

            protectedBytes[^1] ^= 0xFF;

            Assert.ThrowsAny<CryptographicException>(() => ConnectionStringProtector.Unprotect(Convert.ToBase64String(protectedBytes)));
        }
    }
}
