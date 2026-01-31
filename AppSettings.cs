namespace FileIndexer;

public class AppSettings
{
    public string DefaultScanPath { get; set; } = "";
    public string DatabasePath { get; set; } = "fileindex.db";
    public int ScanParallelism { get; set; } = 64;
    public int ScanBatchSize { get; set; } = 500;
}
