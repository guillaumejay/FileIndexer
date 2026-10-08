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
    public bool IsDirectory { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ModifiedAtUtc { get; set; }
    public DateTime IndexedAtUtc { get; set; }
    
    public string SizeFormatted => FormatSize(SizeBytes);

    // Builds the index entry for a file or folder found on disk.
    public static IndexedFile FromFileSystemInfo(FileSystemInfo info, int collectionId)
    {
        var isDirectory = info is DirectoryInfo;
        return new IndexedFile
        {
            CollectionId = collectionId,
            Name = info.Name,
            Path = info.FullName,
            Directory = info switch
            {
                FileInfo file => file.DirectoryName ?? "",
                DirectoryInfo dir => dir.Parent?.FullName ?? "",
                _ => ""
            },
            Extension = isDirectory ? "" : info.Extension.ToLowerInvariant(),
            SizeBytes = info is FileInfo f ? f.Length : 0,
            IsDirectory = isDirectory,
            CreatedAtUtc = info.CreationTimeUtc,
            ModifiedAtUtc = info.LastWriteTimeUtc,
            IndexedAtUtc = DateTime.UtcNow
        };
    }

    public static string FormatSize(long bytes) => bytes switch
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
    
    public string TotalSizeFormatted => IndexedFile.FormatSize(TotalSizeBytes);
}

public class ScanProgress
{
    public int FilesScanned { get; set; }
    public int FilesTotal { get; set; }
    public int DirectoriesScanned { get; set; }
    public int FilesRemoved { get; set; }
    public string CurrentDirectory { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool IsComplete { get; set; }
    public TimeSpan Elapsed { get; set; }
    public int ErrorCount { get; set; }
    
    public double ProgressPercent => FilesTotal > 0 
        ? (double)FilesScanned / FilesTotal * 100 
        : 0;
}
