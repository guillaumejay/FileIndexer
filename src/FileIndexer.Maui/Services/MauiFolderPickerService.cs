using CommunityToolkit.Maui.Storage;

namespace FileIndexer.Maui.Services;

public class MauiFolderPickerService : IMauiFolderPickerService
{
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
            await Application.Current!.MainPage!.DisplayAlert(
                "Not Available",
                "Folder selection is not available on mobile devices.",
                "OK");
#endif
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Folder picker error: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"File picker error: {ex.Message}");
            return null;
        }
    }
}
