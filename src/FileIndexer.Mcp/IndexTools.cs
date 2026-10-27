using System.ComponentModel;
using System.Globalization;
using System.Text;
using FileIndexer.Data;
using FileIndexer.Models;
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

    [McpServerTool(Name = "search_files", ReadOnly = true)]
    [Description("Finds indexed files (not folders) by name or path. Returns full path, size and modification date, best matches first.")]
    public Task<string> SearchFiles(
        [Description(QueryHelp)] string query,
        [Description("Only look inside this folder (and below), e.g. R:\\JDR.")] string? pathPrefix = null,
        [Description("Maximum results (1-500).")] int limit = 50) =>
        SearchAsync(query, pathPrefix, limit, directories: false);

    [McpServerTool(Name = "search_dirs", ReadOnly = true)]
    [Description("Finds indexed folders by name or path. Returns full path and the number of files below each, best matches first.")]
    public Task<string> SearchDirs(
        [Description(QueryHelp)] string query,
        [Description("Only look inside this folder (and below), e.g. R:\\JDR.")] string? pathPrefix = null,
        [Description("Maximum results (1-500).")] int limit = 50) =>
        SearchAsync(query, pathPrefix, limit, directories: true);

    [McpServerTool(Name = "list_dir", ReadOnly = true)]
    [Description("Lists a folder from the index: sub-folders with the number of files below each, then its files. " +
                 "With depth > 1, also lists the sub-folders of sub-folders (folders only).")]
    public async Task<string> ListDir(
        [Description("Folder to list, e.g. R:\\JDR\\D&D and Clones.")] string path,
        [Description("Folder levels to show (1-5).")] int depth = 1,
        [Description("Maximum lines (1-2000).")] int maxEntries = 300)
    {
        if (await paths.ResolveAsync(path) is not { } location)
            return await paths.NotIndexedAsync(path);

        depth = Math.Clamp(depth, 1, 5);
        maxEntries = Math.Clamp(maxEntries, 1, 2000);
        var output = new StringBuilder();
        var lines = 0;
        var truncated = false;

        async Task ListAsync(string directory, int level)
        {
            var children = (await db.SearchWithSortAsync("", SortColumn.Name, SortDirection.Asc, limit: 100_000, directoryFilter: directory)).Files;
            var folders = children.Where(c => c.IsDirectory).ToList();
            var counts = await db.CountFilesUnderAsync(folders.Select(f => f.Path));
            var indent = new string(' ', (level - 1) * 2);

            foreach (var folder in folders)
            {
                if (lines++ >= maxEntries) { truncated = true; return; }
                output.Append(indent).Append(folder.Name).Append("\\  (").Append(counts[folder.Path]).Append(" files)").Append('\n');
                if (level < depth) await ListAsync(folder.Path, level + 1);
                if (truncated) return;
            }
            if (level > 1) return;
            foreach (var file in children.Where(c => !c.IsDirectory))
            {
                if (lines++ >= maxEntries) { truncated = true; return; }
                output.Append(file.Name).Append(" | ").Append(IndexedFile.FormatSize(file.SizeBytes)).Append(" | ").Append(Date(file)).Append('\n');
            }
        }

        await ListAsync(location.Path, 1);
        if (lines == 0) return $"{location.Path}: empty or not in the index (run rescan on it if it was just created).";
        var header = $"{location.Path}{(truncated ? $" (truncated at {maxEntries} lines)" : "")}:";
        return header + "\n" + output;
    }

    [McpServerTool(Name = "rescan", Destructive = false)]
    [Description("Brings the index up to date for a folder (and everything below it) after files were added, moved or deleted. " +
                 "Incremental: only changed entries are rewritten. Waits up to 30 s, then keeps running in the background (see index_status).")]
    public Task<string> Rescan([Description("Folder to rescan, e.g. R:\\JDR or R:\\JDR\\OSR.")] string path) =>
        scans.StartAsync(path, TimeSpan.FromSeconds(30));

    [McpServerTool(Name = "index_status", ReadOnly = true)]
    [Description("Indexed collections and folders, their entry counts and last change, and the state of the last rescan.")]
    public async Task<string> IndexStatus()
    {
        var output = new StringBuilder();
        foreach (var c in await db.GetCollectionsAsync())
        {
            var last = c.LastIndexedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "never";
            output.Append($"{c.Name}: {c.FileCount} entries, last change {last} — {string.Join(", ", c.Paths.Select(p => p.Path))}").Append('\n');
        }
        output.Append(scans.Describe()).Append('\n');
        return output.ToString();
    }

    private async Task<string> SearchAsync(string query, string? pathPrefix, int limit, bool directories)
    {
        string? under = null;
        if (!string.IsNullOrWhiteSpace(pathPrefix))
        {
            if (await paths.ResolveAsync(pathPrefix) is not { } location)
                return await paths.NotIndexedAsync(pathPrefix);
            under = location.Path;
        }

        var result = await db.SearchWithSortAsync(query, SortColumn.Rank, SortDirection.Asc,
            limit: Math.Clamp(limit, 1, MaxResults), showDirectories: directories, underPath: under);
        if (result.Files.Count == 0) return $"No {(directories ? "folder" : "file")} matches \"{query}\".";

        var output = new StringBuilder($"{result.Files.Count} of {result.TotalCount} matches:\n");
        if (directories)
        {
            var counts = await db.CountFilesUnderAsync(result.Files.Select(f => f.Path));
            foreach (var dir in result.Files)
                output.Append(dir.Path).Append(" | ").Append(counts[dir.Path]).Append(" files").Append('\n');
        }
        else
        {
            foreach (var file in result.Files)
                output.Append(file.Path).Append(" | ").Append(IndexedFile.FormatSize(file.SizeBytes)).Append(" | ").Append(Date(file)).Append('\n');
        }
        return output.ToString();
    }

    private static string Date(IndexedFile file) =>
        file.ModifiedAtUtc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
