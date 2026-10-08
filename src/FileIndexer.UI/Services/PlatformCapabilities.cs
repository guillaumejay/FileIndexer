namespace FileIndexer.UI.Services;

// What the host can do. Mobile hosts only browse a synced index: they have no access to the
// indexed file systems, so indexing and file operations are hidden there.
public sealed class PlatformCapabilities
{
    // Indexing, file operations, archive extraction and the activity log.
    public required bool HasFileSystemAccess { get; init; }

    // Touch-first host: long-press copies a path instead of opening a context menu.
    public bool IsTouch { get; init; }
}
