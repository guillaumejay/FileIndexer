using FileIndexer.Data;
using FileIndexer.UI.Services;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Maui.Services;

// Owns the choice of database file. Switching re-points the shared IndexDbContext, so the
// change applies immediately instead of after a restart.
//
// Hosts without file system access (Android/iOS) only get a picked file through a temporary
// cache copy that the OS may purge: the database is copied into the app's own storage instead,
// and selecting again refreshes it from the synced file.
public class DatabaseManager(PlatformCapabilities capabilities, ILogger<DatabaseManager> logger)
{
    private const string DatabasePathKey = "DatabasePath";

    public event Action? Changed;

    public bool CopiesIntoAppStorage => !capabilities.HasFileSystemAccess;

    private static string LocalCopyPath => Path.Combine(FileSystem.AppDataDirectory, IndexDbContext.DefaultFileName);

    // The saved database, if it still exists.
    public string? CurrentPath
    {
        get
        {
            var path = Preferences.Get(DatabasePathKey, null);
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? path : null;
        }
    }

    public bool HasDatabase => CurrentPath != null;

    // Path the shared context is created with at startup (in-memory until one is chosen).
    public string StartupPath => CurrentPath ?? IndexDbContext.InMemory;

    // Lets the user pick a database file. Returns whether the database changed, or the error to show.
    public async Task<(bool Changed, string? Error)> SelectAsync(IndexDbContext db)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select FileIndexer Database",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, ["public.database", "public.data"] },
                    // SQLite files rarely carry a MIME type on Android: allow any file.
                    { DevicePlatform.Android, ["application/octet-stream", "application/x-sqlite3", "*/*"] },
                    { DevicePlatform.WinUI, [".db", ".sqlite", ".sqlite3"] },
                    { DevicePlatform.MacCatalyst, ["public.database", "public.data"] },
                })
            });
            if (result == null) return (false, null);

            if (!CopiesIntoAppStorage)
            {
                Use(db, result.FullPath);
                return (true, null);
            }

            // Release the current copy before overwriting it.
            db.SwitchTo(IndexDbContext.InMemory);
            foreach (var stale in new[] { LocalCopyPath + "-wal", LocalCopyPath + "-shm" })
            {
                File.Delete(stale);
            }
            await using (var source = await result.OpenReadAsync())
            await using (var target = File.Create(LocalCopyPath))
            {
                await source.CopyToAsync(target);
            }
            Use(db, LocalCopyPath);
            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selecting the database failed");
            return (false, $"Cannot open the database: {ex.Message}");
        }
    }

    // Hosts with file system access only: a new, empty database in the chosen folder.
    public void Create(IndexDbContext db, string folder) => Use(db, Path.Combine(folder, IndexDbContext.DefaultFileName));

    private void Use(IndexDbContext db, string path)
    {
        db.SwitchTo(path);
        Preferences.Set(DatabasePathKey, path);
        logger.LogInformation("Using database {Path}", path);
        Changed?.Invoke();
    }
}
