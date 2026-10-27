using System.Globalization;
using FileIndexer.Data;
using FileIndexer.Mcp;
using FileIndexer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP server (stdio) over an existing FileIndexer database: search, browse and rescan the index.
var builder = Host.CreateApplicationBuilder(args);

// Same output (sizes, dates) whatever the machine's language.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

// stdout carries the protocol: logs go to stderr only.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var dbPath = builder.Configuration["db"];
if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
{
    // Opening a missing path would silently create an empty index.
    Console.Error.WriteLine("Usage: FileIndexer.Mcp --db <existing fileindex.db>");
    return 1;
}

builder.Services.AddSingleton(_ => new IndexDbContext(Path.GetFullPath(dbPath)));
builder.Services.AddSingleton<FileScannerService>();
builder.Services.AddSingleton<IndexPaths>();
builder.Services.AddSingleton<ScanJobs>();
builder.Services.AddSingleton<ConfigExportService>();
builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

await builder.Build().RunAsync();
return 0;
