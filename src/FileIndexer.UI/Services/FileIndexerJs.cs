using Microsoft.JSInterop;

namespace FileIndexer.UI.Services;

// Typed wrapper over wwwroot/fileindexer.js, so components never build script strings.
public sealed class FileIndexerJs(IJSRuntime js)
{
    public ValueTask InitColumnResizeAsync(string tableSelector) => js.InvokeVoidAsync("fileIndexer.initColumnResize", tableSelector);

    public ValueTask AdjustContextMenuAsync() => js.InvokeVoidAsync("fileIndexer.adjustContextMenu");

    public ValueTask<bool> IsLightThemeAsync() => js.InvokeAsync<bool>("fileIndexer.isLightTheme");

    public ValueTask SetLightThemeAsync(bool light) => js.InvokeVoidAsync("fileIndexer.setLightTheme", light);

    public ValueTask SelectRenameInputAsync(int selectionEnd) => js.InvokeVoidAsync("fileIndexer.selectRenameInput", selectionEnd);

    public ValueTask FocusAsync(string selector) => js.InvokeVoidAsync("fileIndexer.focus", selector);

    public ValueTask<bool> CopyTextAsync(string text) => js.InvokeAsync<bool>("fileIndexer.copyText", text);

    public ValueTask DownloadTextAsync(string fileName, string contentType, string content) =>
        js.InvokeVoidAsync("fileIndexer.downloadText", fileName, contentType, content);

    public async Task<string?> PickTextFileAsync(string accept) => await js.InvokeAsync<string?>("fileIndexer.pickTextFile", accept);
}
