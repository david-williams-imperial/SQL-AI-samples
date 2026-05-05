// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using Moq;
using Mssql.McpServer;

namespace MssqlMcp.Tests
{
    public sealed class MssqlMcpTests : IDisposable
    {
        /// <summary>
        /// Unique table name for testing.
        /// </summary>
        private readonly string _tableName;

        /// <summary>
        /// Instance of the MCP Server tools.
        /// </summary>
        private readonly Tools _tools;

        public MssqlMcpTests()
        {
            _tableName = $"TestTable_{Guid.NewGuid():N}";

            SqlConnectionFactory connectionFactory = new SqlConnectionFactory();
            Mock<ILogger<Tools>> loggerMock = new Mock<ILogger<Tools>>();

            _tools = new Tools(connectionFactory, loggerMock.Object);
        }

        public void Dispose()
        {
            // Cleanup: Drop the table after each test
            DbOperationResult _ = _tools.DropTable($"DROP TABLE IF EXISTS {_tableName}").GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CreateTable_ReturnsSuccess_WhenSqlIsValid()
        {
            string sql = $"CREATE TABLE {_tableName} (Id INT PRIMARY KEY)";

            DbOperationResult result = await _tools.CreateTable(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task DescribeTable_ReturnsSchema_WhenTableExists()
        {
            // Ensure table exists
            DbOperationResult createResult = await _tools.CreateTable($"CREATE TABLE {_tableName} (Id INT PRIMARY KEY)");

            Assert.NotNull(createResult);
            Assert.True(createResult.Success);

            DbOperationResult result = await _tools.DescribeTable(_tableName);

            Assert.NotNull(result);
            Assert.True(result.Success);

            System.Collections.IDictionary? dict = result.Data as System.Collections.IDictionary;

            Assert.NotNull(dict);
            Assert.True(dict.Contains("table"));
            Assert.True(dict.Contains("columns"));
            Assert.True(dict.Contains("indexes"));
            Assert.True(dict.Contains("constraints"));

            object? table = dict["table"];

            Assert.NotNull(table);

            Type tableType = table.GetType();

            Assert.NotNull(tableType.GetProperty("name"));
            Assert.NotNull(tableType.GetProperty("schema"));

            System.Collections.IEnumerable? columns = dict["columns"] as System.Collections.IEnumerable;

            Assert.NotNull(columns);
        }

        [Fact]
        public async Task DropTable_ReturnsSuccess_WhenSqlIsValid()
        {
            string sql = $"DROP TABLE IF EXISTS {_tableName}";

            DbOperationResult result = await _tools.DropTable(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task InsertData_ReturnsSuccess_WhenSqlIsValid()
        {
            // Ensure table exists
            DbOperationResult createResult = await _tools.CreateTable($"CREATE TABLE {_tableName} (Id INT PRIMARY KEY)");

            Assert.NotNull(createResult);
            Assert.True(createResult.Success);

            string sql = $"INSERT INTO {_tableName} (Id) VALUES (1)";

            DbOperationResult result = await _tools.InsertData(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.RowsAffected.HasValue && result.RowsAffected.Value > 0);
        }

        [Fact]
        public async Task ListTables_ReturnsTables()
        {
            DbOperationResult result = await _tools.ListTables();

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task ReadData_ReturnsData_WhenSqlIsValid()
        {
            // Ensure table exists and has data
            DbOperationResult createResult = await _tools.CreateTable($"CREATE TABLE {_tableName} (Id INT PRIMARY KEY)");

            Assert.NotNull(createResult);
            Assert.True(createResult.Success);

            DbOperationResult insertResult = await _tools.InsertData($"INSERT INTO {_tableName} (Id) VALUES (1)");

            Assert.NotNull(insertResult);
            Assert.True(insertResult.Success);

            string sql = $"SELECT * FROM {_tableName}";

            DbOperationResult result = await _tools.ReadData(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Data);
        }

        [Fact]
        public async Task UpdateData_ReturnsSuccess_WhenSqlIsValid()
        {
            // Ensure table exists and has data
            DbOperationResult createResult = await _tools.CreateTable($"CREATE TABLE {_tableName} (Id INT PRIMARY KEY)");

            Assert.NotNull(createResult);
            Assert.True(createResult.Success);

            DbOperationResult insertResult = await _tools.InsertData($"INSERT INTO {_tableName} (Id) VALUES (1)");

            Assert.NotNull(insertResult);
            Assert.True(insertResult.Success);

            string sql = $"UPDATE {_tableName} SET Id = 2 WHERE Id = 1";

            DbOperationResult result = await _tools.UpdateData(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.RowsAffected.HasValue);
        }

        [Fact]
        public async Task CreateTable_ReturnsError_WhenSqlIsInvalid()
        {
            string sql = "CREATE TABLE";

            DbOperationResult result = await _tools.CreateTable(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DescribeTable_ReturnsError_WhenTableDoesNotExist()
        {
            DbOperationResult result = await _tools.DescribeTable("NonExistentTable");

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Table 'NonExistentTable' not found.", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DropTable_ReturnsError_WhenSqlIsInvalid()
        {
            string sql = "DROP";

            DbOperationResult result = await _tools.DropTable(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InsertData_ReturnsError_WhenSqlIsInvalid()
        {
            string sql = "INSERT INTO TestTable";

            DbOperationResult result = await _tools.InsertData(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ReadData_ReturnsError_WhenSqlIsInvalid()
        {
            string sql = "SELECT FROM";

            DbOperationResult result = await _tools.ReadData(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateData_ReturnsError_WhenSqlIsInvalid()
        {
            string sql = "UPDATE TestTable";

            DbOperationResult result = await _tools.UpdateData(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SqlInjection_NotExecuted_When_QueryFails()
        {
            // Ensure table exists
            DbOperationResult createResult = await _tools.CreateTable($"CREATE TABLE {_tableName} (Id INT PRIMARY KEY, Name NVARCHAR(100))");

            Assert.NotNull(createResult);
            Assert.True(createResult.Success);

            // Attempt SQL Injection
            string maliciousInput = "1; DROP TABLE " + _tableName + "; --";
            string sql = $"INSERT INTO {_tableName} (Id, Name) VALUES ({maliciousInput}, 'Malicious')";

            DbOperationResult result = await _tools.InsertData(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            // Verify table still exists
            DbOperationResult describeResult = await _tools.DescribeTable(_tableName);

            Assert.NotNull(describeResult);
            Assert.True(describeResult.Success);
        }
    }
}
