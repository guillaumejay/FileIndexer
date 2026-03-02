using System.Runtime.InteropServices;
using FileIndexer;
using FileIndexer.Components;
using FileIndexer.Data;
using FileIndexer.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

// Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton(sp => new IndexDbContext(appSettings.DatabasePath));
builder.Services.AddSingleton<FileScannerService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<CollectionService>();
builder.Services.AddScoped<ConfigExportService>();

// Platform-specific services
// Use FallbackFolderPicker on all platforms (custom UI in browser)
builder.Services.AddSingleton<IFolderPickerService, FallbackFolderPicker>();

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    builder.Services.AddSingleton<ITrashService, WindowsTrashService>();
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    builder.Services.AddSingleton<ITrashService, MacTrashService>();
}
else
{
    builder.Services.AddSingleton<ITrashService, LinuxTrashService>();
}

builder.Services.AddSingleton<FileOperationsService>();
builder.Services.AddSingleton<ActivityLogService>();
builder.Services.AddSingleton<ArchiveService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
