> ⚠️ **EXPERIMENTAL USE ONLY** - This MCP Server is provided as an example for educational and experimental purposes only. It is NOT intended for production use. Please use appropriate security measures and thoroughly test before considering any kind of deployment.

# Mssql SQL MCP Server (.NET 8)

This project is a .NET 8 console application implementing a Model Context Protocol (MCP) server for MSSQL Databases using the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Features

- Connects to multiple databases from a single server. Each tool call passes a `databaseKey` to select the database.
- Connection strings are stored in an SQLite configuration database, encrypted with the Windows Data Protection API (DPAPI).
- **MCP Tools Implemented**:
  - ListDatabases: List the configured database keys, with the server and database name behind each.
  - ListTables: List all tables in the database.
  - DescribeTable: Get schema/details for a table.
  - CreateTable: Create new tables.
  - DropTable: Drop existing tables.
  - InsertData: Insert data into tables.
  - ReadData: Read/query data from tables.
  - UpdateData: Update values in tables.
  - GetSqlServerInfo: Get server, authentication, version, and database details for each of a database's connection strings.
  - ListViews, ListFunctions, ListStoredProcedures: List database objects.
  - ExecuteStoredProcedure, ExecuteStoredProcedureWithWriteAccess: Run stored procedures.
- **Logging**: Console logging using Microsoft.Extensions.Logging.
- **Unit Tests**: xUnit-based unit tests for all major components.

## Getting Started

### Prerequisites

- Access to a SQL Server or Azure SQL Database

### Setup

1. **Build ***

---
```sh
   cd MssqlMcp
   dotnet build
```
---


2. **Configure databases**

Each database is identified by a key (e.g. `sales-dev`) and has a read-only connection string and, optionally, a write connection string. Keys without a write connection string are read-only: tools that write will refuse to run against them. Each database also has a description (e.g. "Main 3sixty database"), returned only by `list_databases`, and is tagged with its environment (e.g. Dev, Test, UAT, or Live), which `list_databases` and the server's instructions show the agent; databases added before environments existed, or with `set-database` without one, are "Unspecified". Databases can also be made inactive in the configuration utility, to temporarily withdraw the agent's access without deleting them: inactive databases are not listed by `list_databases`, and other tools return an error saying the database is inactive.

The easiest way to manage databases is the configuration utility (`MssqlMcp.ConfigurationUtility`), a Windows desktop app that lists the configured databases with their connection strings in plain text, and lets you add, edit, and delete them. Connection strings can be typed in or put together with **Build...** (server, database, and Windows or SQL Server authentication), and **Test connection** checks that they work. Deploy it to the same folder as `MssqlMcp.exe` so both use the same configuration database; pass a path on the command line, or use **Change...**, to open a different one. It must be run as the same Windows user as the MCP server.

Alternatively, use the command line. Add or update a database (you will be prompted for the connection strings, which are not echoed):

---
```sh
   MssqlMcp.exe set-database sales-dev
```
---

Other commands:

---
```sh
   MssqlMcp.exe list-databases
   MssqlMcp.exe remove-database sales-dev
```
---

The configuration database is `SQLServerMCP.db` in the same folder as the executable by default. Set the `CONFIG_DATABASE_PATH` environment variable (for the commands above and in the MCP server configuration) to use a different file.

The configuration database's schema is upgraded automatically the first time the MCP server, configuration utility, or command line opens it; applied revisions are recorded in its `DatabaseRevisionHistory` table. A configuration database from a newer version is refused rather than misread. To change the schema, add a `DatabaseVersionN` class implementing `IDatabaseVersion` in `MssqlMcp.Configuration/Upgrade/Versions`, numbered one higher than the latest; it is picked up automatically.

Connection strings are encrypted for the Windows user who saved them, so the configuration database cannot be shared between users or machines, and connection strings must not be entered into it directly. Changes take effect immediately; the MCP server does not need to be restarted, although newly added keys are only listed in the server instructions after a restart.

3. VSCode: **Start VSCode, and add MCP Server config to VSCode Settings**

Load the settings file in VSCode (Ctrl+Shift+P > Preferences: Open Settings (JSON)).

Add a new MCP Server with the following settings:

---
```json
    "MSSQL MCP": {
        "type": "stdio",
        "command": "C:\\src\\MssqlMcp\\MssqlMcp\\bin\\Debug\\net8.0\\MssqlMcp.exe"
}
```
---

NOTE: Replace the path "C:\\src\\SQL-AI-samples" with the location of your SQL-AI-samples repo on your machine.

e.g. your MCP settings should look like this if "MSSQL MCP" is your own MCP Server in VSCode settings:

---
```json
"mcp": {
    "servers": {
        "MSSQL MCP": {
            "type": "stdio",
            "command": "C:\\src\\SQL-AI-samples\\MssqlMcp\\MssqlMcp\\bin\\Debug\\net8.0\\MssqlMcp.exe"
    }
}
```
---

An example of a connection string for Azure SQL Database, to enter when prompted by `set-database`:
---
```
Server=tcp:<servername>.database.windows.net,1433;Initial Catalog=<databasename>;Encrypt=Mandatory;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Interactive
```
---

**Run the MCP Server**

Save the Settings file, and then you should see the "Start" button appear in the settings.json.  Click "Start" to start the MCP Server. (You can then click on "Running" to view the Output window).

Start Chat (Ctrl+Shift+I), make sure Agent Mode is selected.

Click the tools icon, and ensure the "MSSQL MCP" tools are selected.

Then type in the chat window "List the tables in the sales-dev database" and hit enter. (If you have other tools loaded, you may need to specify "MSSQL MCP" in the initial prompt, e.g. "Using MSSQL MCP, list tables").

4. Claude Desktop: **Add MCP Server config to Claude Desktop**

Press File > Settings > Developer.
Press the "Edit Config" button (which will load the claude_desktop_config.json file in your editor).

Add a new MCP Server with the following settings:

---
```json
{
    "mcpServers": {
        "MSSQL MCP": {
            "command": "C:\\src\\SQL-AI-samples\\MssqlMcp\\MssqlMcp\\bin\\Debug\\net8.0\\MssqlMcp.exe"
        }
    }
}
```
---

Save the file, start a new Chat, you'll see the "Tools" icon, it should list 15 MSSQL MCP tools.

# Troubleshooting

1. If you get a "Task canceled" error using "Active Directory Default", try "Active Directory Interactive".



