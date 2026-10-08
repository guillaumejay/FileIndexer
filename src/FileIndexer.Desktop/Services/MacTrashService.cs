namespace FileIndexer.Services;

public class MacTrashService : ITrashService
{
    public bool IsSupported => true;

    public Task<OperationResult> MoveToTrashAsync(string path)
    {
        // Ask Finder to move the item to the trash. Escape for an AppleScript string literal
        // (backslashes first, then quotes); the whole script is passed as a single argument.
        var escapedPath = path.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var script = $"tell application \"Finder\" to delete POSIX file \"{escapedPath}\"";
        return ProcessRunner.RunTrashToolAsync("osascript", "-e", script);
    }
}
