namespace FileIndexer.UI.Services;

// Native dialogs a host may provide. Without INativeFolderPicker the in-page folder browser is
// used; IConfigFileExchange defaults to browser download/upload (JsConfigFileExchange).
public interface INativeFolderPicker
{
    Task<string?> PickFolderAsync();
}

public interface IConfigFileExchange
{
    // Returns false when the user cancelled.
    Task<bool> SaveAsync(string fileName, string content);

    // Returns null when the user cancelled.
    Task<string?> OpenTextAsync();
}

// Browser implementation: download through a blob link, upload through a file input.
public sealed class JsConfigFileExchange(FileIndexerJs js) : IConfigFileExchange
{
    public async Task<bool> SaveAsync(string fileName, string content)
    {
        await js.DownloadTextAsync(fileName, "application/json", content);
        return true;
    }

    public Task<string?> OpenTextAsync() => js.PickTextFileAsync(".json");
}
