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

        private async Task<bool> CreateTestTable()
        {
            string createTableSQL =
                $"CREATE TABLE [dbo].[{_tableName}](" + Environment.NewLine +
                "    [CustomerID] [bigint] IDENTITY(1,1) NOT NULL," + Environment.NewLine +
                "    [CustomerName] [nvarchar](255) NOT NULL," + Environment.NewLine +
                "    [EmailAddress] [nvarchar](400) NULL," + Environment.NewLine +
                "    [IsActive] [bit] NOT NULL," + Environment.NewLine +
                "    [LastUpdated] [datetimeoffset](7) NOT NULL," + Environment.NewLine +
                $" CONSTRAINT [PK_{_tableName}] PRIMARY KEY CLUSTERED" + Environment.NewLine +
                "(" + Environment.NewLine +
                "    [CustomerID] ASC" + Environment.NewLine +
                ")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]" + Environment.NewLine +
                ") ON [PRIMARY];" + Environment.NewLine +
                Environment.NewLine +
                $"ALTER TABLE [dbo].[{_tableName}] ADD  CONSTRAINT [DF_{_tableName}_IsActive]  DEFAULT ((1)) FOR [IsActive];" + Environment.NewLine +
                Environment.NewLine +
                $"ALTER TABLE [dbo].[{_tableName}] ADD  CONSTRAINT [DF_{_tableName}_LastUpdated]  DEFAULT (sysdatetimeoffset()) FOR [LastUpdated];";

            DbOperationResult createTableResult = await _tools.CreateTable(createTableSQL);

            if (createTableResult == null || !createTableResult.Success)
            {
                return false;
            }

            string createIndexSQL = $"CREATE UNIQUE NONCLUSTERED INDEX [IDX_{_tableName}_CustomerName] ON [dbo].[{_tableName}] " + Environment.NewLine +
                "(" + Environment.NewLine +
                "    [CustomerName] ASC " + Environment.NewLine +
                ")WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]";

            DbOperationResult createIndexResult = await _tools.AlterDatabaseSchema(createIndexSQL);

            if (createIndexResult == null || !createIndexResult.Success)
            {
                return false;
            }

            string insertDataSQL = $"INSERT INTO [{_tableName}] (CustomerName, EmailAddress) VALUES (N'Contoso', N'user@example.com')";

            DbOperationResult insertDataResult = await _tools.UpdateData(insertDataSQL);

            if (insertDataResult == null || !insertDataResult.Success)
            {
                return false;
            }

            string insertAdditionalDataSQL = string.Empty;

            for (int i = 0; i < 50; i++)
            {
                insertAdditionalDataSQL += $"INSERT INTO [{_tableName}] (CustomerName) VALUES (N'Customer {Guid.NewGuid()}');";
            }

            DbOperationResult insertAdditionalDataResult = await _tools.UpdateData(insertAdditionalDataSQL);

            if (insertAdditionalDataResult == null || !insertAdditionalDataResult.Success)
            {
                return false;
            }

            return true;
        }

        [Fact]
        public async Task CreateTable_ReturnsSuccess_WhenSqlIsValid()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            // Verify the table is now reachable via DescribeTable
            DbOperationResult describeResult = await _tools.DescribeTable(_tableName);

            Assert.NotNull(describeResult);
            Assert.True(describeResult.Success);
        }

        [Fact]
        public async Task DescribeTable_ReturnsSchema_WhenTableExists()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            DbOperationResult result = await _tools.DescribeTable(_tableName);

            Assert.NotNull(result);
            Assert.True(result.Success);

            System.Collections.IDictionary? dict = result.Data as System.Collections.IDictionary;

            Assert.NotNull(dict);
            Assert.True(dict.Contains("table"));
            Assert.True(dict.Contains("columns"));
            Assert.True(dict.Contains("primaryKeyColumns"));
            Assert.True(dict.Contains("indexes"));
            Assert.True(dict.Contains("constraints"));

            // Validate the table itself
            object? table = dict["table"];

            Assert.NotNull(table);
            Assert.Equal(_tableName, GetPropertyValue<string>(table, "name"));
            Assert.Equal("dbo", GetPropertyValue<string>(table, "schema"));

            // Validate columns against the expected schema
            List<object> columns = ToList(dict["columns"]);

            Assert.Equal(5, columns.Count);

            object customerIDColumn = FindColumn(columns, "CustomerID");

            Assert.Equal("bigint", GetPropertyValue<string>(customerIDColumn, "type"));
            Assert.False(GetPropertyValue<bool>(customerIDColumn, "nullable"));
            Assert.True(GetPropertyValue<bool>(customerIDColumn, "is_identity"));

            object customerNameColumn = FindColumn(columns, "CustomerName");

            Assert.Equal("nvarchar", GetPropertyValue<string>(customerNameColumn, "type"));
            Assert.Equal((short)510, GetPropertyValue<short>(customerNameColumn, "length"));
            Assert.False(GetPropertyValue<bool>(customerNameColumn, "nullable"));
            Assert.False(GetPropertyValue<bool>(customerNameColumn, "is_identity"));

            object emailAddressColumn = FindColumn(columns, "EmailAddress");

            Assert.Equal("nvarchar", GetPropertyValue<string>(emailAddressColumn, "type"));
            Assert.Equal((short)800, GetPropertyValue<short>(emailAddressColumn, "length"));
            Assert.True(GetPropertyValue<bool>(emailAddressColumn, "nullable"));

            object isActiveColumn = FindColumn(columns, "IsActive");

            Assert.Equal("bit", GetPropertyValue<string>(isActiveColumn, "type"));
            Assert.False(GetPropertyValue<bool>(isActiveColumn, "nullable"));
            Assert.True(ValidateDefaultValue(GetPropertyValue<string>(isActiveColumn, "default_value"), "1"));

            object lastUpdatedColumn = FindColumn(columns, "LastUpdated");

            Assert.Equal("datetimeoffset", GetPropertyValue<string>(lastUpdatedColumn, "type"));
            Assert.Equal((byte)7, GetPropertyValue<byte>(lastUpdatedColumn, "scale"));
            Assert.False(GetPropertyValue<bool>(lastUpdatedColumn, "nullable"));
            Assert.True(ValidateDefaultValue(GetPropertyValue<string>(lastUpdatedColumn, "default_value"), "sysdatetimeoffset"));

            // Validate primary key
            List<object> primaryKeyColumns = ToList(dict["primaryKeyColumns"]);

            Assert.Single(primaryKeyColumns);
            Assert.Equal("CustomerID", GetPropertyValue<string>(primaryKeyColumns[0], "column_name"));
            Assert.Equal($"PK_{_tableName}", GetPropertyValue<string>(primaryKeyColumns[0], "constraint_name"));

            // Validate non-PK indexes — the unique nonclustered index on CustomerName
            List<object> indexes = ToList(dict["indexes"]);
            object customerNameIndex = indexes.Single(i => GetPropertyValue<string>(i, "name") == $"IDX_{_tableName}_CustomerName");

            Assert.Equal("NONCLUSTERED", GetPropertyValue<string>(customerNameIndex, "type"));
            Assert.Equal("CustomerName", GetPropertyValue<string>(customerNameIndex, "keys"));

            // Validate key constraints — the primary key
            List<object> constraints = ToList(dict["constraints"]);

            Assert.Contains(constraints, c => GetPropertyValue<string>(c, "name") == $"PK_{_tableName}");
        }

        /// <summary>
        /// Validates that a column's default value is set correctly,
        /// or could not be retrieved due to permissions.
        /// </summary>
        /// <param name="defaultValue">The default value read from the table.</param>
        /// <param name="expected">The expected default value.</param>
        /// <returns>True, if the default value matches or was unobtainable.</returns>
        private bool ValidateDefaultValue(string? defaultValue, string expected)
        {
            if (defaultValue == null)
            {
                return false;
            }

            if (defaultValue.Contains(expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (defaultValue.Equals("<<MCP warning: Set, but unable to retrieve>>"))
            {
                return true;
            }

            return false;
        }

        private static List<object> ToList(object? value)
        {
            System.Collections.IEnumerable? enumerable = value as System.Collections.IEnumerable;

            Assert.NotNull(enumerable);

            return enumerable.Cast<object>().ToList();
        }

        private static List<Dictionary<string, object?>> ToRows(object? value)
        {
            List<Dictionary<string, object?>>? rows = value as List<Dictionary<string, object?>>;

            Assert.NotNull(rows);

            return rows;
        }

        private static object FindColumn(List<object> columns, string name)
        {
            return columns.Single(c => GetPropertyValue<string>(c, "name") == name);
        }

        private static T? GetPropertyValue<T>(object source, string propertyName)
        {
            System.Reflection.PropertyInfo? property = source.GetType().GetProperty(propertyName);

            Assert.NotNull(property);

            object? value = property.GetValue(source);

            if (value == null || value is DBNull)
            {
                return default;
            }

            return (T)value;
        }

        [Fact]
        public async Task DropTable_ReturnsSuccess_WhenTableExists()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            DbOperationResult result = await _tools.DropTable(_tableName);

            Assert.NotNull(result);
            Assert.True(result.Success);

            // Verify the table no longer exists
            DbOperationResult describeResult = await _tools.DescribeTable(_tableName);

            Assert.NotNull(describeResult);
            Assert.False(describeResult.Success);
            Assert.Contains($"Table '{_tableName}' not found.", describeResult.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task InsertData_ReturnsSuccess_WhenSqlIsValid()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            string sql = $"INSERT INTO {_tableName} (CustomerName) VALUES ('Test Customer')";

            DbOperationResult result = await _tools.InsertData(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.RowsAffected.HasValue && result.RowsAffected.Value == 1);

            // Verify the inserted row can be read back
            DbOperationResult readResult = await _tools.ReadData($"SELECT CustomerName FROM {_tableName} WHERE CustomerName = 'Test Customer'");

            Assert.NotNull(readResult);
            Assert.True(readResult.Success);

            List<Dictionary<string, object?>> rows = ToRows(readResult.Data);

            Assert.Single(rows);
            Assert.Equal("Test Customer", rows[0]["CustomerName"]);
        }

        [Fact]
        public async Task ListTables_ReturnsTables()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            DbOperationResult result = await _tools.ListTables();

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Data);

            // Verify the new table appears in the listing
            List<string> tables = (result.Data as IEnumerable<string>)?.ToList() ?? new List<string>();

            Assert.Contains($"dbo.{_tableName}", tables);
        }

        [Fact]
        public async Task ReadData_ReturnsData_WhenSqlIsValid()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            DbOperationResult insertResult = await _tools.InsertData($"INSERT INTO {_tableName} (CustomerName) VALUES ('Test Customer')");

            Assert.NotNull(insertResult);
            Assert.True(insertResult.Success);

            string sql = $"SELECT CustomerID, CustomerName, EmailAddress, IsActive FROM {_tableName} WHERE CustomerName = 'Test Customer'";

            DbOperationResult result = await _tools.ReadData(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.NotNull(result.Data);

            // Verify the inserted row is returned with expected column values and defaults applied
            List<Dictionary<string, object?>> rows = ToRows(result.Data);

            Assert.Single(rows);
            Assert.Equal("Test Customer", rows[0]["CustomerName"]);
            Assert.Null(rows[0]["EmailAddress"]);
            Assert.Equal(true, rows[0]["IsActive"]);
        }

        [Fact]
        public async Task UpdateData_ReturnsSuccess_WhenSqlIsValid()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            DbOperationResult insertResult = await _tools.InsertData($"INSERT INTO {_tableName} (CustomerName) VALUES ('Test Customer')");

            Assert.NotNull(insertResult);
            Assert.True(insertResult.Success);

            string sql = $"UPDATE {_tableName} SET CustomerName = 'Updated Customer' WHERE CustomerName = 'Test Customer'";

            DbOperationResult result = await _tools.UpdateData(sql);

            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.True(result.RowsAffected.HasValue && result.RowsAffected.Value == 1);

            // Verify the row was updated and the original value is gone
            DbOperationResult updatedRowsResult = await _tools.ReadData($"SELECT CustomerName FROM {_tableName} WHERE CustomerName = 'Updated Customer'");

            Assert.NotNull(updatedRowsResult);
            Assert.True(updatedRowsResult.Success);

            List<Dictionary<string, object?>> updatedRows = ToRows(updatedRowsResult.Data);

            Assert.Single(updatedRows);
            Assert.Equal("Updated Customer", updatedRows[0]["CustomerName"]);

            DbOperationResult originalRowsResult = await _tools.ReadData($"SELECT CustomerName FROM {_tableName} WHERE CustomerName = 'Test Customer'");

            Assert.NotNull(originalRowsResult);
            Assert.True(originalRowsResult.Success);
            Assert.Empty(ToRows(originalRowsResult.Data));
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
        public async Task DropTable_ReturnsError_WhenTableDoesNotExist()
        {
            string fakeTableName = "Equipment";

            DbOperationResult result = await _tools.DropTable(fakeTableName);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.Error);
            Assert.Equal($"Table '{fakeTableName}' not found.", result.Error);
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
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            // Attempt SQL Injection via the IsActive numeric column
            string maliciousInput = "1; DROP TABLE " + _tableName + "; --";
            string sql = $"INSERT INTO {_tableName} (CustomerName, IsActive) VALUES ('Malicious', {maliciousInput})";

            DbOperationResult result = await _tools.InsertData(sql);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("syntax", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            // Verify table still exists
            DbOperationResult describeResult = await _tools.DescribeTable(_tableName);

            Assert.NotNull(describeResult);
            Assert.True(describeResult.Success);
        }

        [Fact]
        public async Task ReadData_Fails_When_DestructiveQueryRun()
        {
            // Ensure table exists and has data
            bool createTableResult = await CreateTestTable();

            Assert.True(createTableResult);

            // Attempt drop
            string maliciousInput = $"DROP TABLE {_tableName}";

            DbOperationResult result = await _tools.ReadData(maliciousInput);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.Error);

            Assert.Contains("Cannot drop the table", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("you do not have permission", result.Error, StringComparison.OrdinalIgnoreCase);

            // Verify table still exists
            DbOperationResult describeResult = await _tools.DescribeTable(_tableName);

            Assert.NotNull(describeResult);
            Assert.True(describeResult.Success);
        }
    }
}
