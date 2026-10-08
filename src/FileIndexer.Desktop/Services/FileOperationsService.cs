using System.Diagnostics;
using FileIndexer.Data;
using FileIndexer.Models;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Services;

// File and folder operations that keep the disk and the index in step. Index rows are
// rewritten by path (in every collection that holds them, folder contents included).
public class FileOperationsService
{
    private readonly IndexDbContext _db;
    private readonly ITrashService _trashService;
    private readonly ILogger<FileOperationsService> _logger;

    public FileOperationsService(IndexDbContext db, ITrashService trashService, ILogger<FileOperationsService> logger)
    {
        _db = db;
        _trashService = trashService;
        _logger = logger;
    }

    public async Task<IEnumerable<IndexedFile>> GetFilesByIdsAsync(IEnumerable<long> ids)
    {
        return await _db.GetFilesByIdsAsync(ids);
    }

    public Task<OperationResult> OpenFileAsync(string path)
    {
        if (!Path.Exists(path))
        {
            return Task.FromResult(OperationResult.Failure("Le fichier n'existe plus"));
        }

        try
        {
            using var _ = Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Failure($"Impossible d'ouvrir le fichier : {ex.Message}"));
        }
    }

    public Task<OperationResult> OpenFolderAsync(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory == null || !Directory.Exists(directory))
        {
            return Task.FromResult(OperationResult.Failure("Le dossier n'existe plus"));
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // explorer.exe has its own command-line parsing: "/select," must prefix the quoted
                // path within one argument. Windows paths cannot contain '"', so this is safe.
                using var _ = Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                ProcessRunner.Launch("xdg-open", directory);
            }
            else if (OperatingSystem.IsMacOS())
            {
                ProcessRunner.Launch("open", "-R", path);
            }
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Failure($"Impossible d'ouvrir le dossier : {ex.Message}"));
        }
    }

    public async Task<OperationResult> RenameFileAsync(long fileId, string newName, Func<string, string, Task<ConflictResolution>> onConflict)
    {
        var file = await _db.GetFileByIdAsync(fileId);
        if (file == null)
        {
            return OperationResult.Failure("Fichier non trouvé dans l'index");
        }

        if (!Path.Exists(file.Path))
        {
            return OperationResult.Failure("Le fichier n'existe plus sur le disque");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return OperationResult.Failure("Le nom ne peut pas être vide");
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return OperationResult.Failure("Le nom contient des caractères interdits");
        }

        var directory = Path.GetDirectoryName(file.Path)!;
        var newPath = Path.Combine(directory, newName);
        if (newPath == file.Path)
        {
            return OperationResult.Success();
        }

        var overwrite = false;
        // A case-only rename targets the item itself on case-insensitive file systems: no conflict.
        if (!PathHelper.AreSame(file.Path, newPath))
        {
            var target = await ResolveDestinationAsync(newPath, file.IsDirectory, () => onConflict(file.Name, newName));
            if (target == null)
            {
                return OperationResult.Cancelled();
            }
            (newPath, overwrite) = target.Value;
        }

        try
        {
            await MoveOnDiskAsync(file, newPath, overwrite);
            await _db.MovePathAsync(file.Path, newPath);
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename {Path} to {NewPath}", file.Path, newPath);
            return OperationResult.Failure($"Impossible de renommer : {ex.Message}");
        }
    }

    public Task<OperationResult> CopyFilesAsync(IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict, Action<int, int, string>? onProgress = null) =>
        TransferAsync(move: false, fileIds, destinationFolder, onConflict, onProgress);

    public Task<OperationResult> MoveFilesAsync(IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict, Action<int, int, string>? onProgress = null) =>
        TransferAsync(move: true, fileIds, destinationFolder, onConflict, onProgress);

    private async Task<OperationResult> TransferAsync(bool move, IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict, Action<int, int, string>? onProgress)
    {
        var label = move ? "le déplacement" : "la copie";

        if (!Directory.Exists(destinationFolder))
        {
            return OperationResult.Failure("Le dossier de destination n'existe pas");
        }

        var files = await _db.GetFilesByIdsAsync(fileIds);
        if (files.Count == 0)
        {
            return OperationResult.Failure("Aucun fichier trouvé");
        }

        var errors = new List<FileOperationError>();
        var skipped = 0;
        var succeeded = 0;
        var processed = 0;
        foreach (var file in files)
        {
            onProgress?.Invoke(++processed, files.Count, file.Name);

            if (!Path.Exists(file.Path))
            {
                skipped++;
                _logger.LogWarning("{Operation} skipped: source no longer exists: {Path}", label, file.Path);
                continue;
            }

            if (file.IsDirectory && PathHelper.IsSameOrUnder(destinationFolder, file.Path))
            {
                errors.Add(new FileOperationError(file.Path, "Impossible de placer un dossier dans lui-même"));
                continue;
            }

            var destPath = Path.Combine(destinationFolder, file.Name);
            var overwrite = false;

            if (PathHelper.AreSame(destPath, file.Path))
            {
                if (move)
                {
                    // Already where it should be: nothing to do. Never "replace" an item with itself.
                    skipped++;
                    continue;
                }
                // Copying into its own folder: make a sibling copy, as file managers do.
                destPath = Path.Combine(destinationFolder, PathHelper.GenerateUniqueName(destinationFolder, file.Name, file.IsDirectory));
            }
            else
            {
                var target = await ResolveDestinationAsync(destPath, file.IsDirectory, () => onConflict(file.Name, destPath));
                if (target == null)
                {
                    _logger.LogInformation("{Operation} cancelled by user after {Count} item(s).", label, succeeded);
                    return OperationResult.Cancelled();
                }
                (destPath, overwrite) = target.Value;
            }

            try
            {
                if (move)
                {
                    await MoveOnDiskAsync(file, destPath, overwrite);
                    await _db.MovePathAsync(file.Path, destPath);
                }
                else
                {
                    await CopyOnDiskAsync(file, destPath, overwrite);
                    await IndexCopyAsync(file, destPath, overwrite);
                }
                succeeded++;
            }
            catch (Exception ex)
            {
                errors.Add(new FileOperationError(file.Path, ex.Message));
                _logger.LogError(ex, "Failed {Operation} of {Path} to {Destination}", label, file.Path, destPath);
            }
        }

        return BuildBatchResult(label, succeeded, skipped, errors);
    }

    // Asks the user only when something already exists at destPath. Returns null on Cancel,
    // otherwise the path to write to and whether the existing item must be overwritten.
    private static async Task<(string Path, bool Overwrite)?> ResolveDestinationAsync(string destPath, bool isDirectory, Func<Task<ConflictResolution>> ask)
    {
        if (!Path.Exists(destPath))
        {
            return (destPath, false);
        }

        var directory = Path.GetDirectoryName(destPath)!;
        return await ask() switch
        {
            ConflictResolution.Cancel => null,
            ConflictResolution.Replace => (destPath, true),
            _ => (Path.Combine(directory, PathHelper.GenerateUniqueName(directory, Path.GetFileName(destPath), isDirectory)), false)
        };
    }

    private async Task MoveOnDiskAsync(IndexedFile item, string destPath, bool overwrite)
    {
        if (!item.IsDirectory)
        {
            // Overwrites atomically: the old target is only removed once the move can happen.
            File.Move(item.Path, destPath, overwrite);
            return;
        }

        if (overwrite)
        {
            await TrashExistingAsync(destPath);
        }

        if (string.Equals(Path.GetPathRoot(item.Path), Path.GetPathRoot(destPath), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(item.Path, destPath);
        }
        else
        {
            // Directory.Move cannot cross volumes: copy, then remove the source.
            await Task.Run(() => CopyDirectory(item.Path, destPath));
            Directory.Delete(item.Path, recursive: true);
        }
    }

    private async Task CopyOnDiskAsync(IndexedFile item, string destPath, bool overwrite)
    {
        if (!item.IsDirectory)
        {
            File.Copy(item.Path, destPath, overwrite);
            return;
        }

        if (overwrite)
        {
            await TrashExistingAsync(destPath);
        }
        await Task.Run(() => CopyDirectory(item.Path, destPath));
    }

    // Replacing a folder discards its whole content: send it to the trash so it can be recovered.
    private async Task TrashExistingAsync(string path)
    {
        var result = await _trashService.MoveToTrashAsync(path);
        if (!result.IsSuccess)
        {
            throw new IOException($"Impossible de remplacer {path} : {result.ErrorMessage}");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }

    // Indexes the copy in the source's collection (a folder copy brings its whole content).
    private async Task IndexCopyAsync(IndexedFile source, string destPath, bool overwrite)
    {
        if (overwrite)
        {
            // Rows describing what was just replaced are now wrong.
            await _db.DeletePathsAsync([destPath]);
        }

        IEnumerable<FileSystemInfo> entries = source.IsDirectory
            ? new[] { new DirectoryInfo(destPath) }.Concat(new DirectoryInfo(destPath).EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            : [new FileInfo(destPath)];

        await _db.BulkUpsertAsync(entries.Select(e => IndexedFile.FromFileSystemInfo(e, source.CollectionId)));
    }

    public async Task<OperationResult> DeleteFilesAsync(IEnumerable<long> fileIds, Action<int, int, string>? onProgress = null)
    {
        var files = await _db.GetFilesByIdsAsync(fileIds);
        if (files.Count == 0)
        {
            return OperationResult.Failure("Aucun fichier trouvé");
        }

        var removedPaths = new List<string>();
        var errors = new List<FileOperationError>();
        var skipped = 0;
        var trashed = 0;
        var processed = 0;
        foreach (var file in files)
        {
            onProgress?.Invoke(++processed, files.Count, file.Name);

            if (!Path.Exists(file.Path))
            {
                // Already gone from disk: drop the stale index entry.
                removedPaths.Add(file.Path);
                skipped++;
                _logger.LogWarning("Delete: already absent from disk, removing index entry: {Path}", file.Path);
                continue;
            }

            var result = await _trashService.MoveToTrashAsync(file.Path);
            if (!result.IsSuccess)
            {
                // Keep going so one failure does not abort the whole batch.
                errors.Add(new FileOperationError(file.Path, result.ErrorMessage ?? "Échec de mise à la corbeille"));
                _logger.LogError("Failed to move to trash: {Path}: {Error}", file.Path, result.ErrorMessage);
                continue;
            }
            removedPaths.Add(file.Path);
            trashed++;
        }

        if (removedPaths.Count > 0)
        {
            await _db.DeletePathsAsync(removedPaths);
        }

        return BuildBatchResult("la suppression", trashed, skipped, errors);
    }

    public async Task<EmptyFolderCleanResult> CleanEmptyFoldersAsync(IEnumerable<string> paths)
    {
        var deletedFolders = new List<string>();
        var errorCount = 0;

        await Task.Run(() =>
        {
            foreach (var rootPath in paths)
            {
                if (!Directory.Exists(rootPath))
                    continue;

                CleanEmptyFoldersRecursive(rootPath, deletedFolders, ref errorCount);
            }
        });

        if (deletedFolders.Count > 0)
        {
            await _db.DeletePathsAsync(deletedFolders);
        }

        return new EmptyFolderCleanResult
        {
            DeletedFolders = deletedFolders,
            ErrorCount = errorCount
        };
    }

    private static void CleanEmptyFoldersRecursive(string directory, List<string> deletedFolders, ref int errorCount)
    {
        try
        {
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                CleanEmptyFoldersRecursive(subDir, deletedFolders, ref errorCount);
            }

            // After cleaning subdirectories, check if this directory is now empty
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                try
                {
                    Directory.Delete(directory);
                    deletedFolders.Add(directory);
                }
                catch
                {
                    errorCount++;
                }
            }
        }
        catch
        {
            errorCount++;
        }
    }

    // Turns the per-item tallies of a batch into a single OperationResult: success when nothing
    // failed (items missing on disk are skipped, not failures), otherwise an aggregated error.
    private OperationResult BuildBatchResult(string operationLabel, int succeeded, int skipped, List<FileOperationError> errors)
    {
        if (errors.Count == 0)
        {
            _logger.LogInformation(
                "Completed {Operation}: {Succeeded} succeeded, {Skipped} skipped.",
                operationLabel, succeeded, skipped);
            return new OperationResult
            {
                IsSuccess = true,
                SuccessCount = succeeded,
                SkippedCount = skipped
            };
        }

        var message = $"Échec de {operationLabel} pour {errors.Count} élément(s) sur {succeeded + errors.Count} ; voir les journaux.";
        _logger.LogWarning(
            "Completed {Operation} with errors: {Succeeded} succeeded, {Skipped} skipped, {Failed} failed.",
            operationLabel, succeeded, skipped, errors.Count);
        return new OperationResult
        {
            IsSuccess = false,
            ErrorMessage = message,
            SuccessCount = succeeded,
            SkippedCount = skipped,
            Errors = errors
        };
    }
}

public class OperationResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Number of items processed successfully in a batch operation.</summary>
    public int SuccessCount { get; init; }

    /// <summary>Number of items skipped (no longer on disk, or already at the destination).</summary>
    public int SkippedCount { get; init; }

    /// <summary>Per-item failures collected during a batch; empty when nothing failed.</summary>
    public IReadOnlyList<FileOperationError> Errors { get; init; } = Array.Empty<FileOperationError>();

    public int FailureCount => Errors.Count;

    public static OperationResult Success() => new() { IsSuccess = true };
    public static OperationResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
    public static OperationResult Cancelled() => new() { IsSuccess = false, IsCancelled = true };
}

/// <summary>A single item-level failure inside a batch operation.</summary>
public record FileOperationError(string Path, string Message);

public enum ConflictResolution
{
    Cancel,
    Replace,
    KeepBoth
}
