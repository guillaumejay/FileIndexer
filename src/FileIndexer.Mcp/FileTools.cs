using System.ComponentModel;
using FileIndexer.Data;
using FileIndexer.Models;
using FileIndexer.Services;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FileIndexer.Mcp;

// Changes on disk that keep the index in step (no rescan needed afterwards). Every item is
// recorded in the operations journal.
[McpServerToolType]
public sealed class FileTools(IndexDbContext db, IndexPaths locations, FileOperationsService files)
{
    private const string IfExistsHelp =
        "When the target name is taken: \"skip\" (default), \"keep_both\" (adds \" (1)\") or \"replace\" (a replaced folder goes to the recycle bin).";

    [McpServerTool(Name = "move", Destructive = true)]
    [Description("Moves files or folders into a folder, on disk and in the index (also from one collection to another). One result line per item.")]
    public async Task<string> Move(
        [Description("Files or folders to move.")] string[] paths,
        [Description("Existing folder to move them into.")] string destinationFolder,
        [Description(IfExistsHelp)] string ifExists = "skip")
    {
        var destination = await locations.NormalizeAsync(destinationFolder);
        var conflict = Conflict(ifExists);
        return await EachAsync(paths, async item =>
            Outcome(await files.MoveFilesAsync([item.Id], destination, conflict), $"moved to {destination}"));
    }

    [McpServerTool(Name = "rename", Destructive = true)]
    [Description("Renames a file or folder in place, on disk and in the index.")]
    public async Task<string> Rename(
        [Description("File or folder to rename.")] string path,
        [Description("New name only, not a path.")] string newName,
        [Description(IfExistsHelp)] string ifExists = "skip")
    {
        var conflict = Conflict(ifExists);
        return await EachAsync([path], async item =>
            Outcome(await files.RenameFileAsync(item.Id, newName, conflict), $"renamed to {newName}"));
    }

    [McpServerTool(Name = "delete", Destructive = true)]
    [Description("Sends files or folders to the recycle bin (recoverable) and removes them from the index. One result line per item.")]
    public Task<string> Delete([Description("Files or folders to delete.")] string[] paths) =>
        EachAsync(paths, async item => Outcome(await files.DeleteFilesAsync([item.Id]), "sent to the recycle bin"));

    // One line per item: an item that cannot be found or fails does not stop the others.
    private async Task<string> EachAsync(IEnumerable<string> items, Func<IndexedFile, Task<string>> operation)
    {
        var lines = new List<string>();
        foreach (var path in items)
        {
            try
            {
                lines.Add($"{path}: {await operation(await IndexedItemAsync(path))}");
            }
            catch (McpException ex)
            {
                lines.Add($"{path}: error, {ex.Message}");
            }
        }
        return string.Join('\n', lines) + "\n";
    }

    // The index row for the path. A file on disk that the index has not seen yet (just
    // downloaded) is indexed on the spot; a whole folder needs a rescan first.
    private async Task<IndexedFile> IndexedItemAsync(string path)
    {
        var location = await locations.ResolveAsync(path);
        if (await db.GetFileByPathAsync(location.Path) is { } indexed) return indexed;

        if (File.Exists(location.Path))
        {
            await db.UpsertFilesAsync([IndexedFile.FromFileSystemInfo(new FileInfo(location.Path), location.Collection.Id)]);
            return (await db.GetFileByPathAsync(location.Path))!;
        }
        throw new McpException(Directory.Exists(location.Path)
            ? "folder not indexed yet: rescan its parent first"
            : "not found on disk");
    }

    private static string Outcome(OperationResult result, string success) => result switch
    {
        { IsCancelled: true } => "skipped, the target name is taken (see ifExists)",
        { IsSuccess: false } => $"error, {result.ErrorMessage}",
        { SkippedCount: > 0 } => "skipped, already there or no longer on disk",
        _ => success
    };

    private static Func<string, string, Task<ConflictResolution>> Conflict(string ifExists)
    {
        var resolution = ifExists.Trim().ToLowerInvariant() switch
        {
            "skip" => ConflictResolution.Cancel,
            "keep_both" => ConflictResolution.KeepBoth,
            "replace" => ConflictResolution.Replace,
            _ => throw new McpException($"ifExists must be skip, keep_both or replace, not \"{ifExists}\"")
        };
        return (_, _) => Task.FromResult(resolution);
    }
}
