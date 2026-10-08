using Microsoft.JSInterop;

namespace FileIndexer.UI.Services;

// Typed wrapper over wwwroot/fileindexer.js, so components never build script strings.
// Calls made while no browser is attached (prerendering, or a Web circuit that just
// disconnected) are no-ops returning a neutral value, so components need no guards.
public sealed class FileIndexerJs(IJSRuntime js)
{
    public ValueTask InitColumnResizeAsync(string tableSelector) => Run("fileIndexer.initColumnResize", tableSelector);

    public ValueTask AdjustContextMenuAsync() => Run("fileIndexer.adjustContextMenu");

    public ValueTask<bool> IsLightThemeAsync() => Get("fileIndexer.isLightTheme", false);

    public ValueTask SetLightThemeAsync(bool light) => Run("fileIndexer.setLightTheme", light);

    public ValueTask SelectRenameInputAsync(int selectionEnd) => Run("fileIndexer.selectRenameInput", selectionEnd);

    public ValueTask<bool> CopyTextAsync(string text) => Get("fileIndexer.copyText", false, text);

    public ValueTask DownloadTextAsync(string fileName, string contentType, string content) =>
        Run("fileIndexer.downloadText", fileName, contentType, content);

    public async Task<string?> PickTextFileAsync(string accept) => await Get<string?>("fileIndexer.pickTextFile", null, accept);

    private static bool IsDetached(Exception ex) =>
        ex is JSDisconnectedException or TaskCanceledException
        || (ex is InvalidOperationException && ex.Message.Contains("prerender", StringComparison.OrdinalIgnoreCase));

    private async ValueTask Run(string identifier, params object?[] args)
    {
        try
        {
            await js.InvokeVoidAsync(identifier, args);
        }
        catch (Exception ex) when (IsDetached(ex))
        {
        }
    }

    private async ValueTask<T> Get<T>(string identifier, T fallback, params object?[] args)
    {
        try
        {
            return await js.InvokeAsync<T>(identifier, args);
        }
        catch (Exception ex) when (IsDetached(ex))
        {
            return fallback;
        }
    }
}
