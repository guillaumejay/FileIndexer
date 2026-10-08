namespace FileIndexer.Models;

// Portable description of the collections. Host settings (database path, scan tuning) live in
// each host's own configuration and are not exported; older files carrying a "settings" block
// still import, the block is ignored.
public class ConfigExport
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; }
    public List<ExportedCollection> Collections { get; set; } = new();
}

public class ExportedCollection
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    // Null in exports made before exclusions were exported: the default applies.
    public string? ExcludedDirectories { get; set; }
    public List<string> Paths { get; set; } = new();
}

public enum ImportCollisionStrategy
{
    Skip,
    Rename
}

public class ImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Renamed { get; set; }
    public List<string> Details { get; set; } = new();
}
