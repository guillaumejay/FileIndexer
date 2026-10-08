using FileIndexer.Services;

namespace FileIndexer.UI.Services;

// The desktop services, registered together only when the host has file system access.
// Components resolve this one optional service instead of probing for each.
public sealed class FileSystemFeatures(
    FileScannerService scanner,
    FileOperationsService files,
    ArchiveService archives,
    ActivityLogService activityLog)
{
    public FileScannerService Scanner => scanner;
    public FileOperationsService Files => files;
    public ArchiveService Archives => archives;
    public ActivityLogService ActivityLog => activityLog;
}
