using FileIndexer.Data;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Maui.Services;

// Owns the choice of database file. Switching re-points the shared IndexDbContext, so the
// change applies immediately instead of after a restart.
//
// On Android/iOS a picked file is only readable through a temporary cache copy that the OS may
// purge: the database is copied into the app's own storage instead, and "Select" again refreshes
// it from the synced file.
public class DatabaseManager(ILogger<DatabaseManager> logger)
{
    private const string DatabasePathKey = "DatabasePath";

    public event Action? Changed;

#if DESKTOP
    public static bool CopiesIntoAppStorage => false;
#else
    public static bool CopiesIntoAppStorage => true;
#endif

    private static string LocalCopyPath => Path.Combine(FileSystem.AppDataDirectory, "fileindex.db");

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
    public string StartupPath => CurrentPath ?? ":memory:";

    public async Task<bool> SelectAsync(IndexDbContext db)
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
            if (result == null) return false;

            if (!CopiesIntoAppStorage)
            {
                Use(db, result.FullPath);
                return true;
            }

            // Release the current copy before overwriting it.
            db.SwitchTo(":memory:");
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
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selecting the database failed");
            throw;
        }
    }

    // Desktop only: a new, empty database in the chosen folder.
    public void Create(IndexDbContext db, string folder) => Use(db, Path.Combine(folder, "fileindex.db"));

    private void Use(IndexDbContext db, string path)
    {
        db.SwitchTo(path);
        Preferences.Set(DatabasePathKey, path);
        logger.LogInformation("Using database {Path}", path);
        Changed?.Invoke();
    }
}
