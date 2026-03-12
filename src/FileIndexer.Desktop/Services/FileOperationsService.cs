using System.Diagnostics;
using System.Runtime.InteropServices;
using FileIndexer.Data;
using FileIndexer.Models;

namespace FileIndexer.Services;

public class FileOperationsService
{
    private readonly IndexDbContext _db;
    private readonly ITrashService _trashService;

    public FileOperationsService(IndexDbContext db, ITrashService trashService)
    {
        _db = db;
        _trashService = trashService;
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

    public async Task<OperationResult> CopyFilesAsync(IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict)
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
        foreach (var file in files)
        {
            if (!File.Exists(file.Path))
            {
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
                return OperationResult.Failure($"Erreur lors de la copie de {file.Name} : {ex.Message}");
            }
        }

        if (copiedFiles.Any())
        {
            await _db.InsertFilesAsync(copiedFiles);
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult> MoveFilesAsync(IEnumerable<long> fileIds, string destinationFolder, Func<string, string, Task<ConflictResolution>> onConflict)
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

        foreach (var file in files)
        {
            if (!File.Exists(file.Path))
            {
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
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"Erreur lors du déplacement de {file.Name} : {ex.Message}");
            }
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult> DeleteFilesAsync(IEnumerable<long> fileIds)
    {
        var files = await _db.GetFilesByIdsAsync(fileIds);
        if (!files.Any())
        {
            return OperationResult.Failure("Aucun fichier trouvé");
        }

        var deletedIds = new List<long>();
        foreach (var file in files)
        {
            if (!File.Exists(file.Path))
            {
                deletedIds.Add(file.Id);
                continue;
            }

            var result = await _trashService.MoveToTrashAsync(file.Path);
            if (!result.IsSuccess)
            {
                if (deletedIds.Any())
                {
                    await _db.DeleteFilesByIdsAsync(deletedIds);
                }
                return result;
            }
            deletedIds.Add(file.Id);
        }

        if (deletedIds.Any())
        {
            await _db.DeleteFilesByIdsAsync(deletedIds);
        }

        return OperationResult.Success();
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

    public static OperationResult Success() => new() { IsSuccess = true };
    public static OperationResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
    public static OperationResult Cancelled() => new() { IsSuccess = false, IsCancelled = true };
}

public enum ConflictResolution
{
    Cancel,
    Replace,
    KeepBoth
}

