using CommunityToolkit.Maui;
using FileIndexer.Data;
using FileIndexer.Maui.Services;
using FileIndexer.UI.Services;
using Microsoft.Extensions.Logging;

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

        builder.Services.AddSingleton<DatabaseManager>();
        builder.Services.AddSingleton<IConfigFileExchange, MauiConfigFileExchange>();

#if DESKTOP
        // Windows/macOS: the indexed folders are reachable, so indexing and file operations are on.
        var capabilities = new PlatformCapabilities { HasFileSystemAccess = true };
        builder.Services.AddSingleton<INativeFolderPicker, MauiFolderPicker>();
#else
        // Phones/tablets browse a synced copy of the index.
        var capabilities = new PlatformCapabilities { HasFileSystemAccess = false, IsTouch = true };
#endif

        builder.Services.AddFileIndexer(
            sp => new IndexDbContext(sp.GetRequiredService<DatabaseManager>().StartupPath),
            capabilities);

        return builder.Build();
    }
}
