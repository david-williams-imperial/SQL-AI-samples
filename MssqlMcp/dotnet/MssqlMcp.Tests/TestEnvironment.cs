// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace MssqlMcp.Tests
{
    internal static class TestEnvironment
    {
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void EnsureConnectionStrings()
        {
            string? writeConnectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            string? readConnectionString = Environment.GetEnvironmentVariable("READ_CONNECTION_STRING");

            if (!string.IsNullOrWhiteSpace(writeConnectionString) && string.IsNullOrWhiteSpace(readConnectionString))
            {
                Environment.SetEnvironmentVariable("READ_CONNECTION_STRING", writeConnectionString);
            }
        }
    }
}
