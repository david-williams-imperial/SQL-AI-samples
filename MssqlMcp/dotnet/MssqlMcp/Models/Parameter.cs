// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Mssql.McpServer.Models
{
    public class Parameter
    {
        /// <summary>
        /// The parameter name.
        /// </summary>
        [Required]
        [Description("Name of the parameter, including the leading '@' (e.g. '@CustomerId').")]
        public required string ParameterName { get; set; }

        /// <summary>
        /// The parameter value.
        /// </summary>
        [Description("Value to bind to the parameter. Use a JSON string, number, boolean, or null. Omit or null for output parameters.")]
        public object? ParameterValue { get; set; }

        /// <summary>
        /// Indicates the direction of the parameter.
        /// </summary>
        [DefaultValue(ParameterDirection.Input)]
        [Description("The direction of the parameter. Output parameters will be returned after the procedure executes.")]
        public ParameterDirection ParameterDirection { get; set; }
    }
}
