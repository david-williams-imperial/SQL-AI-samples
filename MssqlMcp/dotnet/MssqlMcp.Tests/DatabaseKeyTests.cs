// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using Mssql.McpServer;

namespace MssqlMcp.Tests
{
    /// <summary>
    /// Tests for selecting databases by key. None of these connect to SQL Server.
    /// </summary>
    public sealed class DatabaseKeyTests
    {
        private const string ReadOnlyConnectionString = "Server=dbserver;Database=Sales;User ID=reader;Password=ReadSecret!";

        private const string WriteConnectionString = "Server=dbserver;Database=Sales;User ID=writer;Password=WriteSecret!";

        /// <summary>
        /// Registry with a read/write database, a read-only database, and an inactive database.
        /// </summary>
        private readonly InMemoryDatabaseRegistry _registry = new InMemoryDatabaseRegistry(
            new DatabaseRegistration("sales", ReadOnlyConnectionString, WriteConnectionString, environmentName: "Live", description: "Main sales database"),
            new DatabaseRegistration("reporting", ReadOnlyConnectionString, null),
            new DatabaseRegistration("archive", ReadOnlyConnectionString, WriteConnectionString, isActive: false));

        /// <summary>
        /// Connection factory under test.
        /// </summary>
        private readonly SqlConnectionFactory _connectionFactory;

        /// <summary>
        /// Tools under test.
        /// </summary>
        private readonly Tools _tools;

        /// <summary>
        /// Initialises the connection factory and tools.
        /// </summary>
        public DatabaseKeyTests()
        {
            _connectionFactory = new SqlConnectionFactory(_registry);
            _tools = new Tools(_connectionFactory, _registry, new Mock<ILogger<Tools>>().Object);
        }

        [Fact]
        public async Task GetReadOnlyConnectionString_SetsReadOnlyIntent()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(await _connectionFactory.GetReadOnlyConnectionStringAsync("sales"));

            Assert.Equal("reader", builder.UserID);
            Assert.Equal(ApplicationIntent.ReadOnly, builder.ApplicationIntent);
        }

        [Fact]
        public async Task GetConnectionString_UsesWriteConnectionString()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(await _connectionFactory.GetConnectionStringAsync("sales"));

            Assert.Equal("writer", builder.UserID);
            Assert.Equal(ApplicationIntent.ReadWrite, builder.ApplicationIntent);
        }

        [Fact]
        public async Task GetConnectionString_Throws_WhenDatabaseIsReadOnly()
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _connectionFactory.GetConnectionStringAsync("reporting"));

            Assert.Contains("read-only", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetConnectionString_ListsConfiguredKeys_WhenKeyIsNotFound()
        {
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _connectionFactory.GetReadOnlyConnectionStringAsync("sale"));

            Assert.Contains("'sale' was not found", ex.Message, StringComparison.Ordinal);
            Assert.Contains("reporting, sales", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("archive", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task GetConnectionString_Throws_WhenDatabaseIsInactive()
        {
            await Assert.ThrowsAsync<DatabaseInactiveException>(() => _connectionFactory.GetReadOnlyConnectionStringAsync("archive"));
            await Assert.ThrowsAsync<DatabaseInactiveException>(() => _connectionFactory.GetConnectionStringAsync("archive"));
        }

        [Fact]
        public async Task ReadData_ReturnsInactiveMessage_WhenDatabaseIsInactive()
        {
            DbOperationResult result = await _tools.ReadData("archive", "SELECT 1");

            Assert.False(result.Success);
            Assert.Equal("archive", result.DatabaseKey);
            Assert.StartsWith("The database 'archive' is currently inactive", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public async Task UpdateData_ReturnsInactiveMessage_WhenDatabaseIsInactive()
        {
            DbOperationResult result = await _tools.UpdateData("archive", "UPDATE x SET y = 1");

            Assert.False(result.Success);
            Assert.Contains("currently inactive", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ToolResults_DoNotIncludeDescription()
        {
            DbOperationResult result = await _tools.ReadData("sale", "SELECT 1");

            string json = JsonSerializer.Serialize(result);

            Assert.DoesNotContain("Main sales database", json, StringComparison.Ordinal);
            Assert.DoesNotContain("description", json, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task GetSqlServerInfo_ReturnsInactiveMessage_WhenDatabaseIsInactive()
        {
            DbOperationResult result = await _tools.GetSqlServerInfo("archive");

            Assert.False(result.Success);
            Assert.Equal("archive", result.DatabaseKey);
            Assert.Contains("currently inactive", result.Error, StringComparison.Ordinal);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task ReadData_ReturnsError_WhenKeyIsNotFound()
        {
            DbOperationResult result = await _tools.ReadData("sale", "SELECT 1");

            Assert.False(result.Success);
            Assert.Equal("sale", result.DatabaseKey);
            Assert.Null(result.DatabaseName);
            Assert.Contains("Configured keys: reporting, sales", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadData_ReturnsError_WhenKeyIsBlank()
        {
            DbOperationResult result = await _tools.ReadData(" ", "SELECT 1");

            Assert.False(result.Success);
        }

        [Fact]
        public async Task UpdateData_ReturnsError_WhenDatabaseIsReadOnly()
        {
            DbOperationResult result = await _tools.UpdateData("reporting", "UPDATE x SET y = 1");

            Assert.False(result.Success);
            Assert.Contains("read-only", result.Error, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ListDatabases_ReturnsAllKeys_WithoutPasswords()
        {
            DbOperationResult result = await _tools.ListDatabases();

            string json = JsonSerializer.Serialize(result.Data);

            Assert.True(result.Success);
            Assert.Contains("\"database_key\":\"reporting\"", json, StringComparison.Ordinal);
            Assert.Contains("\"database_key\":\"sales\"", json, StringComparison.Ordinal);
            Assert.Contains("Sales", json, StringComparison.Ordinal);
            Assert.DoesNotContain("Secret", json, StringComparison.Ordinal);
            Assert.DoesNotContain("archive", json, StringComparison.Ordinal);
            Assert.Contains("\"database_key\":\"sales\",\"environment\":\"Live\",\"description\":\"Main sales database\"", json, StringComparison.Ordinal);
            Assert.Contains("\"database_key\":\"reporting\",\"environment\":\"Unspecified\"", json, StringComparison.Ordinal);
        }
    }
}
