namespace FileIndexer.Models;

public class EmptyFolderCleanResult
{
    public List<string> DeletedFolders { get; init; } = new();
    public int ErrorCount { get; init; }
}
