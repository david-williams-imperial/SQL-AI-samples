// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mssql.McpServer;

[McpServerToolType]
public partial class Tools(ISqlConnectionFactory connectionFactory, ILogger<Tools> logger)
{
    private readonly ISqlConnectionFactory _connectionFactory = connectionFactory;

    private readonly ILogger<Tools> _logger = logger;

}