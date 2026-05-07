// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.Text.Json.Serialization;

namespace Mssql.McpServer.Models
{
    /// <summary>
    /// The direction of the SQL parameter.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ParameterDirection>))]
    public enum ParameterDirection
    {
        /// <summary>
        /// Input parameter.
        /// </summary>
        Input = 0,
        /// <summary>
        /// Output parameter.
        /// </summary>
        Output = 1,
        /// <summary>
        /// Input and output parameter.
        /// </summary>
        InputOutput = 2,
        /// <summary>
        /// Return value parameter.
        /// </summary>
        ReturnValue = 3
    }
}
