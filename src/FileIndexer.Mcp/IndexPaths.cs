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
        var full = Path.GetFullPath(path.Trim().Trim('"'));
        var roots = (await db.GetCollectionsAsync(withStats: false))
            .SelectMany(c => c.Paths.Select(p => (Collection: c, Root: p.Path)))
            .ToList();
        var match = roots
            .Where(x => PathHelper.IsSameOrUnder(full, x.Root))
            .OrderByDescending(x => x.Root.Length)
            .FirstOrDefault();
        if (match.Collection == null)
            throw new McpException($"{path} is not inside an indexed collection. Indexed folders: {string.Join(", ", roots.Select(r => r.Root))}");
        return new IndexedLocation(match.Collection, PathHelper.WithDiskCase(full, match.Root));
    }
}
