using FileIndexer.Data;
using FileIndexer.Models;
using ModelContextProtocol;

namespace FileIndexer.Mcp;

public readonly record struct IndexedLocation(Collection Collection, string Path);

// Maps a path as typed ("r:/jdr/d&d and clones") to the collection that holds it and to the
// spelling the index stores.
public sealed class IndexPaths(IndexDbContext db)
{
    // Throws a tool error listing the indexed folders when the path is in no collection.
    public async Task<IndexedLocation> ResolveAsync(string path)
    {
        var (location, roots) = await FindAsync(path);
        return location ?? throw new McpException(
            $"{path} is not inside an indexed collection. Indexed folders: {string.Join(", ", roots)}");
    }

    // The path as the index spells it when it lies in a collection, otherwise just made absolute.
    public async Task<string> NormalizeAsync(string path) =>
        (await FindAsync(path)).Location?.Path ?? Full(path);

    private async Task<(IndexedLocation? Location, IEnumerable<string> Roots)> FindAsync(string path)
    {
        var full = Full(path);
        var roots = (await db.GetCollectionsAsync(withStats: false))
            .SelectMany(c => c.Paths.Select(p => (Collection: c, Root: p.Path)))
            .ToList();
        var match = roots
            .Where(x => PathHelper.IsSameOrUnder(full, x.Root))
            .OrderByDescending(x => x.Root.Length)
            .FirstOrDefault();
        var location = match.Collection == null
            ? (IndexedLocation?)null
            : new IndexedLocation(match.Collection, PathHelper.WithDiskCase(full, match.Root));
        return (location, roots.Select(r => r.Root));
    }

    private static string Full(string path) => Path.GetFullPath(path.Trim().Trim('"'));
}
