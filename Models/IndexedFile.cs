namespace FileIndexer.Models;

public enum SortColumn
{
    Name,
    Directory,
    Extension,
    Size,
    ModifiedAt,
    Rank
}

public enum SortDirection
{
    Asc,
    Desc
}

public class IndexedFile
{
    public long Id { get; set; }
    public int CollectionId { get; set; }
    public required string Name { get; set; }
    public required string Path { get; set; }
    public required string Directory { get; set; }
    public required string Extension { get; set; }
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ModifiedAtUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; }
    
    public string SizeFormatted => FormatSize(SizeBytes);
    
    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public class SearchResult
{
    public required List<IndexedFile> Files { get; set; }
    public int TotalCount { get; set; }
    public TimeSpan SearchDuration { get; set; }
}

public class IndexStats
{
    public int TotalFiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public Dictionary<string, int> FilesByExtension { get; set; } = new();
    public DateTime? LastIndexedAtUtc { get; set; }
    
    public string TotalSizeFormatted => TotalSizeBytes switch
    {
        < 1024 => $"{TotalSizeBytes} B",
        < 1024 * 1024 => $"{TotalSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{TotalSizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{TotalSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public class ScanProgress
{
    public int FilesScanned { get; set; }
    public int FilesTotal { get; set; }
    public int DirectoriesScanned { get; set; }
    public string CurrentDirectory { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool IsComplete { get; set; }
    public TimeSpan Elapsed { get; set; }
    public int ErrorCount { get; set; }
    
    public double ProgressPercent => FilesTotal > 0 
        ? (double)FilesScanned / FilesTotal * 100 
        : 0;
}
