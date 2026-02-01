namespace FileIndexer.Services;

/// <summary>
/// Fallback folder picker that signals to the UI to show a custom folder browser component.
/// This is used on all platforms as the native dialog approach requires Windows-specific packages.
/// </summary>
public class FallbackFolderPicker : IFolderPickerService
{
    public bool IsNativeDialogSupported => false;

    public event Func<string?, Task<string?>>? OnPickerRequested;

    public async Task<string?> PickFolderAsync(string? initialPath = null)
    {
        if (OnPickerRequested != null)
        {
            return await OnPickerRequested(initialPath);
        }
        return null;
    }
}
