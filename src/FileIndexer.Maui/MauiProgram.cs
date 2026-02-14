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
