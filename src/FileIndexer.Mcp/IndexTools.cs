using System.ComponentModel;
using System.Text;
using FileIndexer.Data;
using FileIndexer.Models;
using FileIndexer.Services;
using ModelContextProtocol.Server;

namespace FileIndexer.Mcp;

[McpServerToolType]
public sealed class IndexTools(IndexDbContext db, IndexPaths paths, ScanJobs scans)
{
    private const int MaxResults = 500;
    private const string QueryHelp =
        "Words (any order, all required) matched as prefixes against file names and the folders above them. " +
        "Case, accents, '_' and punctuation are ignored; \"D&D\", \"DnD\" and \"D and D\" are the same. " +
        "Use a|b for alternatives, e.g. \"dnd|dungeons\".";
    private const string PathPrefixHelp = "Only look inside this folder (and below), e.g. R:\\JDR.";
    private const string LimitHelp = "Maximum results (1-500).";

    [McpServerTool(Name = "search_files", ReadOnly = true)]
    [Description("Finds indexed files (not folders) by name or path. Returns full path, size and modification date, best matches first.")]
    public Task<string> SearchFiles(
        [Description(QueryHelp)] string query,
        [Description(PathPrefixHelp)] string? pathPrefix = null,
        [Description(LimitHelp)] int limit = 50) =>
        SearchAsync(query, pathPrefix, limit, directories: false);

    [McpServerTool(Name = "search_dirs", ReadOnly = true)]
    [Description("Finds indexed folders by name or path. Returns full path and the number of files below each, best matches first.")]
    public Task<string> SearchDirs(
        [Description(QueryHelp)] string query,
        [Description(PathPrefixHelp)] string? pathPrefix = null,
        [Description(LimitHelp)] int limit = 50) =>
        SearchAsync(query, pathPrefix, limit, directories: true);

    [McpServerTool(Name = "list_dir", ReadOnly = true)]
    [Description("Lists a folder from the index: sub-folders with the number of files below each, then its files. " +
                 "With depth > 1, also lists the sub-folders of sub-folders (folders only).")]
    public async Task<string> ListDir(
        [Description("Folder to list, e.g. R:\\JDR\\D&D and Clones.")] string path,
        [Description("Folder levels to show (1-5).")] int depth = 1,
        [Description("Maximum lines (1-2000).")] int maxEntries = 300)
    {
        var root = (await paths.ResolveAsync(path)).Path;
        depth = Math.Clamp(depth, 1, 5);
        maxEntries = Math.Clamp(maxEntries, 1, 2000);
        var lines = new List<string>();

        // Depth-first, folders only below the first level; stops once maxEntries + 1 lines exist
        // (the extra one only tells that the listing was cut).
        async Task ListFoldersAsync(string directory, int level)
        {
            var folders = await db.GetChildrenAsync(directory, directories: true, limit: maxEntries + 1 - lines.Count);
            var counts = await db.CountFilesUnderAsync(folders.Select(f => f.Path));
            foreach (var folder in folders)
            {
                if (lines.Count > maxEntries) return;
                lines.Add($"{new string(' ', (level - 1) * 2)}{folder.Name}\\ ({counts[folder.Path]} files)");
                if (level < depth) await ListFoldersAsync(folder.Path, level + 1);
            }
        }

        await ListFoldersAsync(root, 1);
        if (lines.Count <= maxEntries)
        {
            var files = await db.GetChildrenAsync(root, directories: false, limit: maxEntries + 1 - lines.Count);
            lines.AddRange(files.Select(f => FileLine(f, f.Name)));
        }

        if (lines.Count == 0) return $"{root}: empty or not in the index (run rescan on it if it was just created).";
        var truncated = lines.Count > maxEntries;
        return $"{root}{(truncated ? $" (truncated at {maxEntries} lines)" : "")}:\n{string.Join('\n', lines.Take(maxEntries))}\n";
    }

    [McpServerTool(Name = "rescan", Destructive = false)]
    [Description("Brings the index up to date for a folder (and everything below it) after files were added, moved or deleted. " +
                 "Incremental: only changed entries are rewritten. Waits up to 30 s, then keeps running in the background (see index_status).")]
    public Task<string> Rescan([Description("Folder to rescan, e.g. R:\\JDR or R:\\JDR\\OSR.")] string path) =>
        scans.StartAsync(path, TimeSpan.FromSeconds(30));

    [McpServerTool(Name = "index_status", ReadOnly = true)]
    [Description("Indexed collections and folders, their entry counts and last change, the state of the last rescan, and how many moves, renames and deletes were made (7 days and all time).")]
    public async Task<string> IndexStatus()
    {
        var output = new StringBuilder();
        foreach (var c in await db.GetCollectionsAsync())
        {
            var last = c.LastIndexedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "never";
            output.Append($"{c.Name}: {c.FileCount} entries, last change {last} — {string.Join(", ", c.Paths.Select(p => p.Path))}\n");
        }
        output.Append(scans.Describe()).Append('\n');
        output.Append($"Operations, last 7 days: {Counts(await db.GetOperationCountsAsync(DateTime.UtcNow.AddDays(-7)))}\n");
        output.Append($"Operations, all time: {Counts(await db.GetOperationCountsAsync())}\n");
        return output.ToString();
    }

    private async Task<string> SearchAsync(string query, string? pathPrefix, int limit, bool directories)
    {
        var under = string.IsNullOrWhiteSpace(pathPrefix) ? null : (await paths.ResolveAsync(pathPrefix)).Path;
        var result = await db.SearchWithSortAsync(query, SortColumn.Rank, SortDirection.Asc,
            limit: Math.Clamp(limit, 1, MaxResults), showDirectories: directories, underPath: under);
        if (result.Files.Count == 0) return $"No {(directories ? "folder" : "file")} matches \"{query}\".";

        var counts = directories ? await db.CountFilesUnderAsync(result.Files.Select(f => f.Path)) : null;
        var lines = result.Files.Select(f => counts != null ? $"{f.Path} | {counts[f.Path]} files" : FileLine(f, f.Path));
        return $"{result.Files.Count} of {result.TotalCount} matches:\n{string.Join('\n', lines)}\n";
    }

    private static string Counts(List<OperationCount> counts) => counts.Count == 0
        ? "none"
        : string.Join(", ", counts.Select(c => $"{c.Operation} {c.Succeeded}{(c.Failed > 0 ? $" ({c.Failed} failed)" : "")}"));

    private static string FileLine(IndexedFile file, string label) =>
        $"{label} | {file.SizeFormatted} | {file.ModifiedAtUtc.ToLocalTime():yyyy-MM-dd}";
}
