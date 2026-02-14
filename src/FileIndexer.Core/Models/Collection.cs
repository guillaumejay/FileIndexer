namespace FileIndexer.Models;

public class Collection
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<CollectionPath> Paths { get; set; } = new();

    // Stats (populated separately)
    public int FileCount { get; set; }
    public DateTime? LastIndexedAtUtc { get; set; }
}

public class CollectionPath
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public required string Path { get; set; }
}

public class PathOverlap
{
    public required string Path { get; set; }
    public required string CollectionName { get; set; }
    public int CollectionId { get; set; }
    public bool IsParent { get; set; }  // true if existing path is parent of new path
}
