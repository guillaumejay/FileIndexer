namespace FileIndexer.Maui.Services;

public interface IMauiFolderPickerService
{
    Task<string?> PickFolderAsync(string? initialPath = null);
    Task<string?> PickDatabaseFileAsync();
}
