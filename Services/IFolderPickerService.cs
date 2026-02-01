namespace FileIndexer.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(string? initialPath = null);
    bool IsNativeDialogSupported { get; }
}
