using FileIndexer;
using FileIndexer.Components;
using FileIndexer.Data;
using FileIndexer.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();
var databasePath = ResolveDatabasePath(appSettings.DatabasePath, builder.Environment.ContentRootPath);

// Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The server runs on the machine that holds the files: full file system access. Folder
// picking uses the in-page browser, config files go through browser download/upload.
builder.Services.AddFileIndexer(
    _ => new IndexDbContext(databasePath),
    new PlatformCapabilities { HasFileSystemAccess = true },
    scanner =>
    {
        scanner.DegreeOfParallelism = appSettings.ScanParallelism;
        scanner.BatchSize = appSettings.ScanBatchSize;
    });
builder.Services.AddScoped<IConfigFileExchange, JsConfigFileExchange>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Anchor a relative database path to the app content root so it no longer depends on the
// process working directory (which varies with the launch point). ":memory:" and absolute
// paths are passed through unchanged.
static string ResolveDatabasePath(string configured, string baseDir)
{
    if (string.IsNullOrWhiteSpace(configured))
        configured = "fileindex.db";
    if (configured == ":memory:" || Path.IsPathRooted(configured))
        return configured;
    return Path.GetFullPath(configured, baseDir);
}
