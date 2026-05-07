// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Mssql.McpServer.Models;

namespace Mssql.McpServer;

public partial class Tools
{
    [McpServerTool(
        Title = "Execute Stored Procedure (read-only)",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false),
        Description("Executes a stored procedure using a read-only database connection. " +
                    "Use this for procedures that only read data. " +
                    "Multiple result sets may be returned. " +
                    "Output parameters will be returned.")]
    public Task<DbOperationResult> ExecuteStoredProcedure(
        [Description("Name of the stored procedure (optionally schema-qualified, e.g. 'dbo.GetCustomer').")] string storedProcedureName,
        [Description("Input and output parameters to pass to the stored procedure. Omit or pass null if the procedure takes no parameters.")] List<Parameter>? parameters)
    {
        return ExecuteStoredProcedureCore(storedProcedureName, parameters, readOnly: true);
    }

    [McpServerTool(
        Title = "Execute Stored Procedure (write access)",
        ReadOnly = false,
        Idempotent = false,
        Destructive = true),
        Description("Executes a stored procedure using a database connection with write access. " +
                    "Use this for procedures that modify data. " +
                    "Multiple result sets may be returned. " +
                    "Output parameters will be returned.")]
    public Task<DbOperationResult> ExecuteStoredProcedureWithWriteAccess(
        [Description("Name of the stored procedure (optionally schema-qualified, e.g. 'dbo.UpdateCustomer').")] string storedProcedureName,
        [Description("Input and output parameters to pass to the stored procedure. Omit or pass null if the procedure takes no parameters.")] List<Parameter>? parameters)
    {
        return ExecuteStoredProcedureCore(storedProcedureName, parameters, readOnly: false);
    }

    private async Task<DbOperationResult> ExecuteStoredProcedureCore(string storedProcedureName, List<Parameter>? parameters, bool readOnly)
    {
        if (string.IsNullOrWhiteSpace(storedProcedureName))
        {
            return new DbOperationResult(success: false, error: "Stored procedure name cannot be null/empty/whitespace");
        }

        SqlConnection connection;

        try
        {
            connection = readOnly
                ? await _connectionFactory.GetOpenReadOnlyConnectionAsync()
                : await _connectionFactory.GetOpenConnectionAsync();
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
                Dictionary<string, object> outputs = new Dictionary<string, object>();

                Dictionary<string, List<Dictionary<string, object?>>> resultSets = new Dictionary<string, List<Dictionary<string, object?>>>();

                await using (SqlCommand queryCommand = connection.CreateCommand())
                {
                    queryCommand.CommandType = CommandType.StoredProcedure;
                    queryCommand.CommandText = storedProcedureName;

                    if (parameters != null)
                    {
                        foreach (Parameter itm in parameters)
                        {
                            string? formattedParameterName = FormatParameterName(itm.ParameterName);

                            if (formattedParameterName == null)
                            {
                                continue;
                            }

                            SqlParameter parameter = new SqlParameter
                            {
                                ParameterName = formattedParameterName,
                                Value = DBNull.Value
                            };

                            switch (itm.ParameterDirection)
                            {
                                case Models.ParameterDirection.Input:
                                    parameter.Direction = System.Data.ParameterDirection.Input;
                                    break;
                                case Models.ParameterDirection.Output:
                                    parameter.Direction = System.Data.ParameterDirection.Output;
                                    break;
                                case Models.ParameterDirection.InputOutput:
                                    parameter.Direction = System.Data.ParameterDirection.InputOutput;
                                    break;
                                case Models.ParameterDirection.ReturnValue:
                                    parameter.Direction = System.Data.ParameterDirection.ReturnValue;
                                    break;
                            }

                            if (parameter.Direction == System.Data.ParameterDirection.Output ||
                                parameter.Direction == System.Data.ParameterDirection.InputOutput)
                            {
                                parameter.Size = -1;
                            }

                            if (itm.ParameterValue != null)
                            {
                                if (itm.ParameterValue is JsonElement jsonElement)
                                {
                                    parameter.Value = jsonElement.ValueKind switch
                                    {
                                        JsonValueKind.Null or JsonValueKind.Undefined => DBNull.Value,
                                        JsonValueKind.String => (object)(jsonElement.GetString() ?? (object)DBNull.Value),
                                        JsonValueKind.True => true,
                                        JsonValueKind.False => false,
                                        JsonValueKind.Number when jsonElement.TryGetInt32(out int i) => i,
                                        JsonValueKind.Number when jsonElement.TryGetInt64(out long l) => l,
                                        JsonValueKind.Number when jsonElement.TryGetDecimal(out decimal d) => d,
                                        JsonValueKind.Number => jsonElement.GetDouble(),
                                        JsonValueKind.Array or JsonValueKind.Object => jsonElement.GetRawText(),
                                        _ => DBNull.Value,
                                    };
                                }
                                else
                                {
                                    parameter.Value = itm.ParameterValue;
                                }
                            }

                            queryCommand.Parameters.Add(parameter);
                        }
                    }

                    await using (SqlDataReader reader = await queryCommand.ExecuteReaderAsync())
                    {
                        int currentResultSet = 0;

                        do
                        {
                            List<Dictionary<string, object?>> rows = new List<Dictionary<string, object?>>();

                            while (await reader.ReadAsync())
                            {
                                Dictionary<string, object?> row = new Dictionary<string, object?>(reader.FieldCount);

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                }

                                rows.Add(row);
                            }

                            resultSets.Add($"result_set_{currentResultSet}", rows);

                            currentResultSet++;
                        }
                        while (await reader.NextResultAsync());
                    }

                    outputs.Add("result_sets", resultSets);

                    Dictionary<string, object> outputParameters = new Dictionary<string, object>();

                    foreach (SqlParameter param in queryCommand.Parameters)
                    {
                        if (param.Direction == System.Data.ParameterDirection.Output || param.Direction == System.Data.ParameterDirection.InputOutput || param.Direction == System.Data.ParameterDirection.ReturnValue)
                        {
                            outputParameters.Add(param.ParameterName, param.Value);
                        }
                    }

                    outputs.Add("output_parameters", outputParameters);
                }

                return new DbOperationResult(success: true, data: outputs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteStoredProcedure failed: {Message}", ex.Message);

            return new DbOperationResult(success: false, error: ex.Message);
        }
    }

    private string? FormatParameterName(string? parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        parameterName = parameterName.Trim();

        if (!parameterName.StartsWith('@'))
        {
            parameterName = "@" + parameterName;
        }

        return parameterName;
    }
}
