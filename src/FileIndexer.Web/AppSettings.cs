namespace FileIndexer;

public class AppSettings
{
    public string DatabasePath { get; set; } = FileIndexer.Data.IndexDbContext.DefaultFileName;
    public int ScanParallelism { get; set; } = 64;
    public int ScanBatchSize { get; set; } = 500;
}
