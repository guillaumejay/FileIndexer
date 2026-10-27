using FileIndexer.Data;
using FileIndexer.Models;

namespace FileIndexer.Mcp;

public readonly record struct IndexedLocation(Collection Collection, string Path);

// Maps a path as typed ("r:/jdr/d&d and clones") to the collection that holds it and to the
// spelling the index stores. Paths are compared byte for byte in SQL, so the case must match.
public sealed class IndexPaths(IndexDbContext db)
{
    public async Task<IndexedLocation?> ResolveAsync(string path)
    {
        var full = Path.GetFullPath(path.Trim().Trim('"'));
        var match = (await db.GetCollectionsAsync())
            .SelectMany(c => c.Paths.Select(p => (Collection: c, Root: p.Path)))
            .Where(x => PathHelper.IsSameOrUnder(full, x.Root))
            .OrderByDescending(x => x.Root.Length)
            .FirstOrDefault();
        return match.Collection == null ? null : new IndexedLocation(match.Collection, Canonical(full, match.Root));
    }

    public async Task<string> NotIndexedAsync(string path) =>
        $"{path} is not inside an indexed collection. Indexed folders: " +
        string.Join(", ", (await db.GetCollectionsAsync()).SelectMany(c => c.Paths).Select(p => p.Path));

    // The root as configured, then each segment as named on disk: the scanner records FullName
    // built from the configured root, so this is what the index holds. A segment that cannot be
    // read (offline, deleted) is kept as typed.
    internal static string Canonical(string full, string root)
    {
        var current = Path.TrimEndingDirectorySeparator(root);
        foreach (var segment in Path.GetRelativePath(root, full).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            string? onDisk = null;
            try
            {
                onDisk = new DirectoryInfo(current).EnumerateFileSystemInfos(segment).FirstOrDefault()?.Name;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            current = Path.Combine(current, onDisk ?? segment);
        }
        return current;
    }
}
