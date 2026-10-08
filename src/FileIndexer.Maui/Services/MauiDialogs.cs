using System.Text;
using CommunityToolkit.Maui.Storage;
using FileIndexer.UI.Services;

namespace FileIndexer.Maui.Services;

// Native folder picker (desktop targets only: mobile hosts never pick folders).
public class MauiFolderPicker : INativeFolderPicker
{
    public async Task<string?> PickFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(default);
        return result.IsSuccessful ? result.Folder.Path : null;
    }
}

public class MauiConfigFileExchange : IConfigFileExchange
{
    public async Task<bool> SaveAsync(string fileName, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var result = await FileSaver.Default.SaveAsync(fileName, stream, default);
        return result.IsSuccessful;
    }

    public async Task<string?> OpenTextAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select configuration file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".json"] },
                { DevicePlatform.macOS, ["json"] },
                { DevicePlatform.MacCatalyst, ["public.json"] },
                { DevicePlatform.iOS, ["public.json"] },
                { DevicePlatform.Android, ["application/json"] }
            })
        });
        if (file == null) return null;

        await using var stream = await file.OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
