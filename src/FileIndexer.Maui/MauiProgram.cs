using CommunityToolkit.Maui;
using FileIndexer.Data;
using FileIndexer.Services;
using FileIndexer.Maui.Services;
using Microsoft.Extensions.Logging;

#if DESKTOP
using System.Runtime.InteropServices;
#endif

namespace FileIndexer.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Workaround for a .NET MAUI bug: ConfigureEnvironmentVariables strips the
        // DOTNET_ / ASPNETCORE_ prefixes and adds the remainder to a case-insensitive
        // config dictionary. When both DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT are
        // set they both reduce to "ENVIRONMENT", throwing "An item with the same key has
        // already been added. Key: ENVIRONMENT". Drop the redundant one before building.
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is not null &&
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") is not null)
        {
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        }

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Database path service
        builder.Services.AddSingleton<DatabasePathService>();

        // Core services
        builder.Services.AddSingleton<IndexDbContext>(sp =>
        {
            var dbPathService = sp.GetRequiredService<DatabasePathService>();
            var dbPath = dbPathService.GetDatabasePath();
            return new IndexDbContext(dbPath ?? ":memory:");
        });

        builder.Services.AddScoped<SearchService>();
        builder.Services.AddScoped<CollectionService>();
        builder.Services.AddScoped<ConfigExportService>();
        builder.Services.AddSingleton<BuildInfoService>();

#if DESKTOP
        // Desktop-only services (Windows/macOS)
        RegisterDesktopServices(builder.Services);
#endif

        // Platform-specific folder picker
        builder.Services.AddSingleton<IMauiFolderPickerService, MauiFolderPickerService>();

        return builder.Build();
    }

#if DESKTOP
    private static void RegisterDesktopServices(IServiceCollection services)
    {
        // File scanner for indexing
        services.AddSingleton<FileScannerService>();

        // File operations
        services.AddSingleton<FileOperationsService>();

        // Activity log & archive services
        services.AddSingleton<ActivityLogService>();
        services.AddSingleton<ArchiveService>();

        // Platform-specific trash service
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<ITrashService, WindowsTrashService>();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            services.AddSingleton<ITrashService, MacTrashService>();
        }
    }
#endif
}
