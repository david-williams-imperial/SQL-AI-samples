// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

public partial class Tools
{
    private const string TableInfoQuery =
        "SELECT t.object_id AS id, t.name, s.name AS [schema], p.value AS description, t.type, u.name AS owner " +
        "FROM sys.tables t " +
        "INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
        "LEFT JOIN sys.extended_properties p ON p.major_id = t.object_id AND p.minor_id = 0 AND p.name = 'MS_Description' " +
        "LEFT JOIN sys.sysusers u ON t.principal_id = u.uid " +
        "WHERE t.name = @TableName AND (s.name = @TableSchema OR @TableSchema IS NULL)";

    private const string ColumnsQuery =
        "SELECT c.[name], ty.[name] AS [type], c.[max_length] AS [length_bytes], " +
        "CASE WHEN c.[max_length] = -1 AND ty.[name] LIKE N'%char%' THEN N'MAX' " +
        "WHEN ty.[name] IN (N'nvarchar', N'nchar') THEN CAST ((c.[max_length] / 2) as nvarchar(50)) " +
        "WHEN ty.[name] IN (N'varchar', N'char') THEN CAST (c.[max_length] as nvarchar(50)) ELSE N'N/A' END As [string_length], " +
        "c.[precision], c.scale, c.is_nullable AS nullable, c.is_identity, p.[value] AS [description], c.default_object_id, object_definition(c.default_object_id) as default_value " +
        "FROM sys.columns c " +
        "INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id " +
        "LEFT JOIN sys.extended_properties p ON p.major_id = c.object_id AND p.minor_id = c.column_id AND p.name = 'MS_Description' " +
        "WHERE c.object_id = (" +
        "  SELECT object_id FROM sys.tables t " +
        "  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
        "  WHERE t.name = @TableName AND (s.name = @TableSchema OR @TableSchema IS NULL)" +
        ")";

    private const string IndexesQuery =
        "SELECT i.name, i.type_desc AS type, p.value AS description, " +
        "  STUFF((SELECT ',' + c.name FROM sys.index_columns ic " +
        "    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id " +
        "    WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 1, '') AS keys " +
        "FROM sys.indexes i " +
        "LEFT JOIN sys.extended_properties p ON p.major_id = i.object_id AND p.minor_id = i.index_id AND p.name = 'MS_Description' " +
        "WHERE i.object_id = (" +
        "  SELECT object_id FROM sys.tables t " +
        "  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
        "  WHERE t.name = @TableName AND (s.name = @TableSchema OR @TableSchema IS NULL)" +
        ") AND i.is_primary_key = 0 AND i.is_unique_constraint = 0";

    private const string ConstraintsQuery =
        "SELECT kc.name, kc.type_desc AS type, " +
        "  STUFF((SELECT ',' + c.name FROM sys.index_columns ic " +
        "    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id " +
        "    WHERE ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 1, '') AS keys " +
        "FROM sys.key_constraints kc " +
        "WHERE kc.parent_object_id = (" +
        "  SELECT object_id FROM sys.tables t " +
        "  INNER JOIN sys.schemas s ON t.schema_id = s.schema_id " +
        "  WHERE t.name = @TableName AND (s.name = @TableSchema OR @TableSchema IS NULL)" +
        ")";

    private const string ForeignKeysQuery =
      "SELECT fk.name AS name, " +
      "  SCHEMA_NAME(tp.schema_id) AS [schema], " +
      "  tp.name AS table_name, " +
      "  STUFF((SELECT ', ' + cp.name " +
      "         FROM sys.foreign_key_columns AS fkc2 " +
      "         JOIN sys.columns AS cp ON fkc2.parent_object_id = cp.object_id " +
      "           AND fkc2.parent_column_id = cp.column_id " +
      "         WHERE fkc2.constraint_object_id = fk.object_id " +
      "         ORDER BY fkc2.constraint_column_id " +
      "         FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS column_names, " +
      "  SCHEMA_NAME(tr.schema_id) AS referenced_schema, " +
      "  tr.name AS referenced_table, " +
      "  STUFF((SELECT ', ' + cr.name " +
      "         FROM sys.foreign_key_columns AS fkc2 " +
      "         JOIN sys.columns AS cr ON fkc2.referenced_object_id = cr.object_id " +
      "           AND fkc2.referenced_column_id = cr.column_id " +
      "         WHERE fkc2.constraint_object_id = fk.object_id " +
      "         ORDER BY fkc2.constraint_column_id " +
      "         FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS referenced_column_names " +
      "FROM sys.foreign_keys AS fk " +
      "JOIN sys.tables AS tp ON fk.parent_object_id = tp.object_id " +
      "JOIN sys.tables AS tr ON fk.referenced_object_id = tr.object_id " +
      "WHERE (SCHEMA_NAME(tp.schema_id) = @TableSchema OR @TableSchema IS NULL) " +
      "  AND tp.name = @TableName";

    private const string PrimaryKeyQuery =
        "SELECT kcu.column_name, kcu.ORDINAL_POSITION as ordinal_position, kcu.CONSTRAINT_NAME " +
        "FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc " +
        "INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME " +
        "WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND kcu.table_name = @TableName " +
        "AND (kcu.TABLE_SCHEMA = @TableSchema OR @TableSchema IS NULL) " +
        "ORDER BY kcu.ORDINAL_POSITION ASC";

    [McpServerTool(
        Title = "Describe Table",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Returns the schema of a SQL Server table, including columns, indexes, primary key, constraints, and foreign keys.")]
    public async Task<DbOperationResult> DescribeTable(
        [Description("Name of the table, optionally prefixed with the schema (e.g. dbo.mytable)")] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new DbOperationResult(success: false, error: "Table name parameter cannot be null/empty/whitespace");
        }

        string? schema = null;

        if (name.Contains('.'))
        {
            string[] parts = name.Split('.', 2);
            schema = parts[0];
            name = parts[1];
        }

        SqlConnection connection;

        try
        {
            connection = await _connectionFactory.GetOpenReadOnlyConnectionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: $"Failed to connect to database: {ex.Message}");
        }

        try
        {
            await using (connection)
            {
                Dictionary<string, object> result = new Dictionary<string, object>();

                await using (SqlCommand tableInfoCommand = connection.CreateCommand())
                {
                    tableInfoCommand.CommandText = TableInfoQuery;

                    tableInfoCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    tableInfoCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    await using (SqlDataReader reader = await tableInfoCommand.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        if (await reader.ReadAsync())
                        {
                            result["table"] = new
                            {
                                id = reader["id"],
                                name = reader["name"],
                                schema = reader["schema"],
                                owner = reader["owner"],
                                type = reader["type"],
                                description = reader["description"] is DBNull ? null : reader["description"]
                            };
                        }
                        else
                        {
                            return new DbOperationResult(success: false, error: $"Table '{name}' not found.");
                        }
                    }
                }

                await using (SqlCommand columnsCommand = connection.CreateCommand())
                {
                    columnsCommand.CommandText = ColumnsQuery;

                    columnsCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    columnsCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    List<object> columns = new List<object>();

                    await using (SqlDataReader columnsReader = await columnsCommand.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        while (await columnsReader.ReadAsync())
                        {
                            int defaultObjectID = (int) columnsReader["default_object_id"];
                            string? defaultValue = null;

                            if (defaultObjectID != 0)
                            {
                                object? defaultValueObj = columnsReader["default_value"];

                                if (defaultValueObj != null)
                                {
                                    defaultValue = defaultValueObj as string;
                                }

                                if (string.IsNullOrWhiteSpace(defaultValue))
                                {
                                    defaultValue = "<<MCP warning: Set, but unable to retrieve>>";
                                }
                            }

                            columns.Add(new
                            {
                                name = columnsReader["name"],
                                type = columnsReader["type"],
                                length_bytes = columnsReader["length_bytes"],
                                string_length = columnsReader["string_length"],
                                precision = columnsReader["precision"],
                                scale = columnsReader["scale"],
                                nullable = (bool)columnsReader["nullable"],
                                is_identity = (bool)columnsReader["is_identity"],
                                description = columnsReader["description"] is DBNull ? null : columnsReader["description"],
                                default_value = defaultValue
                            });
                        }
                    }

                    result["columns"] = columns;
                }

                await using (SqlCommand primarykeyCommand = connection.CreateCommand())
                {
                    primarykeyCommand.CommandText = PrimaryKeyQuery;

                    primarykeyCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    primarykeyCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    List<object> primaryKeyColumns = new List<object>();

                    await using (SqlDataReader primaryKeyReader = await primarykeyCommand.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        while (await primaryKeyReader.ReadAsync())
                        {
                            primaryKeyColumns.Add(new
                            {
                                column_name = primaryKeyReader["column_name"],
                                ordinal_position = primaryKeyReader["ordinal_position"],
                                constraint_name = primaryKeyReader["constraint_name"]
                            });
                        }
                    }

                    result["primaryKeyColumns"] = primaryKeyColumns;
                }

                await using (SqlCommand indexesCommand = connection.CreateCommand())
                {
                    indexesCommand.CommandText = IndexesQuery;

                    indexesCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    indexesCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    List<object> indexes = new List<object>();

                    await using (SqlDataReader indexesReader = await indexesCommand.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        while (await indexesReader.ReadAsync())
                        {
                            indexes.Add(new
                            {
                                name = indexesReader["name"],
                                type = indexesReader["type"],
                                description = indexesReader["description"] is DBNull ? null : indexesReader["description"],
                                keys = indexesReader["keys"]
                            });
                        }
                    }

                    result["indexes"] = indexes;
                }

                await using (SqlCommand constraintsCommand = connection.CreateCommand())
                {
                    constraintsCommand.CommandText = ConstraintsQuery;

                    constraintsCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    constraintsCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    List<object> constraints = new List<object>();

                    await using (SqlDataReader constraintsReader = await constraintsCommand.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        while (await constraintsReader.ReadAsync())
                        {
                            constraints.Add(new
                            {
                                name = constraintsReader["name"],
                                type = constraintsReader["type"],
                                keys = constraintsReader["keys"]
                            });
                        }
                    }

                    result["constraints"] = constraints;
                }

                await using (SqlCommand foreignKeysCommand = connection.CreateCommand())
                {
                    foreignKeysCommand.CommandText = ForeignKeysQuery;

                    foreignKeysCommand.Parameters.Add(new SqlParameter("@TableName", SqlDbType.NVarChar) { Value = name });
                    foreignKeysCommand.Parameters.Add(new SqlParameter("@TableSchema", SqlDbType.NVarChar) { Value = schema ?? DBNull.Value as object });

                    List<object> foreignKeys = new List<object>();

                    await using (SqlDataReader foreignKeysReader = await foreignKeysCommand.ExecuteReaderAsync(CommandBehavior.SingleResult))
                    {
                        while (await foreignKeysReader.ReadAsync())
                        {
                            foreignKeys.Add(new
                            {
                                name = foreignKeysReader["name"],
                                schema = foreignKeysReader["schema"],
                                table_name = foreignKeysReader["table_name"],
                                column_names = foreignKeysReader["column_names"],
                                referenced_schema = foreignKeysReader["referenced_schema"],
                                referenced_table = foreignKeysReader["referenced_table"],
                                referenced_column_names = foreignKeysReader["referenced_column_names"]
                            });
                        }
                    }

                    result["foreignKeys"] = foreignKeys;
                }

                return new DbOperationResult(success: true, data: result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DescribeTable failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }
}
