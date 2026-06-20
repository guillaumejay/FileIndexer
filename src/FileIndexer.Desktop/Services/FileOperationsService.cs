using System.Diagnostics;
using System.Runtime.InteropServices;
using FileIndexer.Data;
using FileIndexer.Models;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Services;

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

    public async Task<OperationResult> OpenFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return OperationResult.Failure("Le fichier n'existe plus");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Impossible d'ouvrir le fichier : {ex.Message}");
        }
    }

    public async Task<OperationResult> OpenFolderAsync(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory == null || !Directory.Exists(directory))
        {
            return OperationResult.Failure("Le dossier n'existe plus");
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", directory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"-R \"{path}\"");
            }
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Impossible d'ouvrir le dossier : {ex.Message}");
        }
    }

    public async Task<OperationResult> RenameFileAsync(long fileId, string newName, Func<string, string, Task<ConflictResolution>> onConflict)
    {
        var file = await _db.GetFileByIdAsync(fileId);
        if (file == null)
        {
            return OperationResult.Failure("Fichier non trouvé dans l'index");
        }

        if (!File.Exists(file.Path))
        {
            return OperationResult.Failure("Le fichier n'existe plus sur le disque");
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (newName.IndexOfAny(invalidChars) >= 0)
        {
            return OperationResult.Failure("Le nom contient des caractères interdits");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            return OperationResult.Failure("Le nom ne peut pas être vide");
        }

        var directory = Path.GetDirectoryName(file.Path)!;
        var newPath = Path.Combine(directory, newName);

        if (File.Exists(newPath) && !string.Equals(file.Path, newPath, StringComparison.OrdinalIgnoreCase))
        {
            var resolution = await onConflict(file.Name, newName);
            switch (resolution)
            {
                case ConflictResolution.Cancel:
                    return OperationResult.Cancelled();
                case ConflictResolution.Replace:
                    File.Delete(newPath);
                    break;
                case ConflictResolution.KeepBoth:
                    newName = GenerateUniqueName(directory, newName);
                    newPath = Path.Combine(directory, newName);
                    break;
            }
        }

        try
        {
            File.Move(file.Path, newPath);
            await _db.UpdateFilePathAsync(fileId, newPath, directory, newName, Path.GetExtension(newName));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Impossible de renommer : {ex.Message}");
        }
    }

    public async Task<OperationResult> CopyFilesAsync(IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict, Action<int, int, string>? onProgress = null)
    {
        if (!Directory.Exists(destinationFolder))
        {
            return OperationResult.Failure("Le dossier de destination n'existe pas");
        }

        var files = await _db.GetFilesByIdsAsync(fileIds);
        if (!files.Any())
        {
            return OperationResult.Failure("Aucun fichier trouvé");
        }

        var copiedFiles = new List<IndexedFile>();
        var errors = new List<FileOperationError>();
        var skipped = 0;
        var processed = 0;
        foreach (var file in files)
        {
            onProgress?.Invoke(++processed, files.Count, file.Name);

            if (!File.Exists(file.Path))
            {
                skipped++;
                _logger.LogWarning("Copy skipped: source file no longer exists: {Path}", file.Path);
                continue;
            }

            var destPath = Path.Combine(destinationFolder, file.Name);
            var finalName = file.Name;

            if (File.Exists(destPath))
            {
                var resolution = await onConflict(file.Name, destPath);
                switch (resolution)
                {
                    case ConflictResolution.Cancel:
                        // Persist what was already copied before aborting so the index stays consistent.
                        if (copiedFiles.Count > 0)
                        {
                            await _db.InsertFilesAsync(copiedFiles);
                        }
                        _logger.LogInformation("Copy cancelled by user after {Count} file(s).", copiedFiles.Count);
                        return OperationResult.Cancelled();
                    case ConflictResolution.Replace:
                        File.Delete(destPath);
                        break;
                    case ConflictResolution.KeepBoth:
                        finalName = GenerateUniqueName(destinationFolder, file.Name);
                        destPath = Path.Combine(destinationFolder, finalName);
                        break;
                }
            }

            try
            {
                File.Copy(file.Path, destPath);
                var fileInfo = new FileInfo(destPath);
                copiedFiles.Add(new IndexedFile
                {
                    CollectionId = file.CollectionId,
                    Name = finalName,
                    Path = destPath,
                    Directory = destinationFolder,
                    Extension = file.Extension,
                    SizeBytes = fileInfo.Length,
                    CreatedAtUtc = fileInfo.CreationTimeUtc,
                    ModifiedAtUtc = fileInfo.LastWriteTimeUtc,
                    IndexedAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                errors.Add(new FileOperationError(file.Path, ex.Message));
                _logger.LogError(ex, "Failed to copy {Path} to {Destination}", file.Path, destPath);
            }
        }

        if (copiedFiles.Count > 0)
        {
            await _db.InsertFilesAsync(copiedFiles);
        }

        return BuildBatchResult("la copie", copiedFiles.Count, skipped, errors);
    }

    public async Task<OperationResult> MoveFilesAsync(IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict, Action<int, int, string>? onProgress = null)
    {
        if (!Directory.Exists(destinationFolder))
        {
            return OperationResult.Failure("Le dossier de destination n'existe pas");
        }

        var files = await _db.GetFilesByIdsAsync(fileIds);
        if (!files.Any())
        {
            return OperationResult.Failure("Aucun fichier trouvé");
        }

        var errors = new List<FileOperationError>();
        var skipped = 0;
        var moved = 0;
        var processed = 0;
        foreach (var file in files)
        {
            onProgress?.Invoke(++processed, files.Count, file.Name);

            if (!File.Exists(file.Path))
            {
                skipped++;
                _logger.LogWarning("Move skipped: source file no longer exists: {Path}", file.Path);
                continue;
            }

            var destPath = Path.Combine(destinationFolder, file.Name);
            var finalName = file.Name;

            if (File.Exists(destPath))
            {
                var resolution = await onConflict(file.Name, destPath);
                switch (resolution)
                {
                    case ConflictResolution.Cancel:
                        _logger.LogInformation("Move cancelled by user after {Count} file(s).", moved);
                        return OperationResult.Cancelled();
                    case ConflictResolution.Replace:
                        File.Delete(destPath);
                        break;
                    case ConflictResolution.KeepBoth:
                        finalName = GenerateUniqueName(destinationFolder, file.Name);
                        destPath = Path.Combine(destinationFolder, finalName);
                        break;
                }
            }

            try
            {
                File.Move(file.Path, destPath);
                await _db.UpdateFilePathAsync(file.Id, destPath, destinationFolder, finalName, Path.GetExtension(finalName));
                moved++;
            }
            catch (Exception ex)
            {
                errors.Add(new FileOperationError(file.Path, ex.Message));
                _logger.LogError(ex, "Failed to move {Path} to {Destination}", file.Path, destPath);
            }
        }

        return BuildBatchResult("le déplacement", moved, skipped, errors);
    }

    public async Task<OperationResult> DeleteFilesAsync(IEnumerable<long> fileIds, Action<int, int, string>? onProgress = null)
    {
        var files = await _db.GetFilesByIdsAsync(fileIds);
        if (!files.Any())
        {
            return OperationResult.Failure("Aucun fichier trouvé");
        }

        var deletedIds = new List<long>();
        var errors = new List<FileOperationError>();
        var skipped = 0;
        var trashed = 0;
        var processed = 0;
        foreach (var file in files)
        {
            onProgress?.Invoke(++processed, files.Count, file.Name);

            if (!File.Exists(file.Path))
            {
                // Already gone from disk: drop the stale index entry.
                deletedIds.Add(file.Id);
                skipped++;
                _logger.LogWarning("Delete: file already absent from disk, removing index entry: {Path}", file.Path);
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
            deletedIds.Add(file.Id);
            trashed++;
        }

        if (deletedIds.Count > 0)
        {
            await _db.DeleteFilesByIdsAsync(deletedIds);
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

    // Turns the per-file tallies of a batch into a single OperationResult: success when nothing
    // failed (files missing on disk are skipped, not failures), otherwise an aggregated error.
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

        var message = $"Échec de {operationLabel} pour {errors.Count} fichier(s) sur {succeeded + errors.Count} ; voir les journaux.";
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

    private static string GenerateUniqueName(string directory, string fileName)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var counter = 1;

        string newName;
        do
        {
            newName = $"{nameWithoutExt} ({counter}){ext}";
            counter++;
        } while (File.Exists(Path.Combine(directory, newName)));

        return newName;
    }
}

public class OperationResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Number of files processed successfully in a batch operation.</summary>
    public int SuccessCount { get; init; }

    /// <summary>Number of files skipped because they no longer existed on disk.</summary>
    public int SkippedCount { get; init; }

    /// <summary>Per-file failures collected during a batch; empty when nothing failed.</summary>
    public IReadOnlyList<FileOperationError> Errors { get; init; } = Array.Empty<FileOperationError>();

    public int FailureCount => Errors.Count;

    public static OperationResult Success() => new() { IsSuccess = true };
    public static OperationResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
    public static OperationResult Cancelled() => new() { IsSuccess = false, IsCancelled = true };
}

/// <summary>A single file-level failure inside a batch operation.</summary>
public record FileOperationError(string Path, string Message);

public enum ConflictResolution
{
    Cancel,
    Replace,
    KeepBoth
}

