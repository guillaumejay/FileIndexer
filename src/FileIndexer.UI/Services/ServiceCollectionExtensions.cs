using FileIndexer.Data;
using FileIndexer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileIndexer.UI.Services;

public static class ServiceCollectionExtensions
{
    // Registers everything the shared UI needs. The desktop services (scanner, file operations,
    // archives, activity log, trash) are added only when the host has file system access.
    // Hosts register their own INativeFolderPicker / IConfigFileExchange before calling this.
    public static IServiceCollection AddFileIndexer(
        this IServiceCollection services,
        Func<IServiceProvider, IndexDbContext> database,
        PlatformCapabilities capabilities,
        Action<FileScannerService>? configureScanner = null)
    {
        services.AddSingleton(capabilities);
        services.AddSingleton<IndexDbContext>(database);
        services.AddSingleton<BuildInfoService>();
        services.AddScoped<SearchService>();
        services.AddScoped<CollectionService>();
        services.AddScoped<ConfigExportService>();
        services.AddScoped<FileIndexerJs>();
        services.AddScoped<UiFeedback>();
        services.TryAddScoped<IConfigFileExchange, JsConfigFileExchange>();

        if (capabilities.HasFileSystemAccess)
        {
            services.AddSingleton(sp =>
            {
                var scanner = ActivatorUtilities.CreateInstance<FileScannerService>(sp);
                configureScanner?.Invoke(scanner);
                return scanner;
            });
            services.AddSingleton<FileOperationsService>();
            services.AddSingleton<ActivityLogService>();
            services.AddSingleton<ArchiveService>();
            services.AddSingleton<FileSystemFeatures>();
            services.TryAddSingleton<ITrashService>(_ =>
                OperatingSystem.IsWindows() ? new WindowsTrashService()
                : OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst() ? new MacTrashService()
                : new LinuxTrashService());
        }

        return services;
    }
}
