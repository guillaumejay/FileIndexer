namespace FileIndexer.Models;

public class ConfigExport
{
    public int Version { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; }
    public ExportedSettings? Settings { get; set; }
    public List<ExportedCollection> Collections { get; set; } = new();
}

public class ExportedSettings
{
    public string DefaultScanPath { get; set; } = "";
    public int ScanParallelism { get; set; } = 64;
    public int ScanBatchSize { get; set; } = 500;
}

public class ExportedCollection
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
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
