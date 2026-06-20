using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Maui.Services;

public class MauiFolderPickerService : IMauiFolderPickerService
{
    private readonly ILogger<MauiFolderPickerService> _logger;

    public MauiFolderPickerService(ILogger<MauiFolderPickerService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> PickFolderAsync(string? initialPath = null)
    {
        try
        {
#if WINDOWS
            // Use native Windows folder picker
            var result = await FolderPicker.Default.PickAsync(default);
            if (result.IsSuccessful)
            {
                return result.Folder.Path;
            }
#elif MACCATALYST
            // Use native macOS folder picker
            var result = await FolderPicker.Default.PickAsync(default);
            if (result.IsSuccessful)
            {
                return result.Folder.Path;
            }
#else
            // On mobile, we don't support folder picking for scan
            // This should not be called on mobile
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
            {
                await page.DisplayAlertAsync(
                    "Not Available",
                    "Folder selection is not available on mobile devices.",
                    "OK");
            }
#endif
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder picker failed");
            return null;
        }
    }

    public async Task<string?> PickDatabaseFileAsync()
    {
        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.iOS, new[] { "public.database", "public.data" } },
                { DevicePlatform.Android, new[] { "application/octet-stream", "application/x-sqlite3", "*/*" } },
                { DevicePlatform.WinUI, new[] { ".db", ".sqlite", ".sqlite3" } },
                { DevicePlatform.MacCatalyst, new[] { "public.database", "public.data" } },
            });

            var options = new PickOptions
            {
                PickerTitle = "Select FileIndexer Database",
                FileTypes = customFileType
            };

            var result = await FilePicker.Default.PickAsync(options);

            return result?.FullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File picker failed");
            return null;
        }
    }
}
