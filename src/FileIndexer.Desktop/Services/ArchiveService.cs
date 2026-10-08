using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace FileIndexer.Services;

public class ArchiveService
{
    private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tgz", ".tbz2", ".txz"
    };

    private readonly ILogger<ArchiveService> _logger;

    public ArchiveService(ILogger<ArchiveService> logger)
    {
        _logger = logger;
    }

    public static bool IsArchive(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (ArchiveExtensions.Contains(ext))
            return true;

        // Handle double extensions like .tar.gz
        if (ext.Equals(".gz", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".bz2", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".xz", StringComparison.OrdinalIgnoreCase))
        {
            var withoutExt = Path.GetFileNameWithoutExtension(fileName);
            var innerExt = Path.GetExtension(withoutExt);
            if (innerExt.Equals(".tar", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public async Task<ArchiveExtractResult> ExtractSmartAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
        {
            _logger.LogWarning("Extraction skipped: archive does not exist: {Path}", archivePath);
            return ArchiveExtractResult.Failure("Le fichier archive n'existe pas");
        }

        var archiveDir = Path.GetDirectoryName(archivePath)!;
        var archiveNameWithoutExt = GetArchiveNameWithoutExtension(archivePath);

        try
        {
            _logger.LogInformation("Extracting archive {Path}", archivePath);
            return await Task.Run(() =>
            {
                using var archive = ArchiveFactory.Open(archivePath);
                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();

                if (entries.Count == 0)
                {
                    _logger.LogWarning("Archive is empty: {Path}", archivePath);
                    return ArchiveExtractResult.Failure("L'archive est vide");
                }

                // Analyze root level structure
                var singleRootFolder = GetSingleRootFolder(archive);
                string extractDir;

                if (singleRootFolder != null && !Path.Exists(Path.Combine(archiveDir, singleRootFolder)))
                {
                    // Extract at the same level as the archive (the single root folder acts as container).
                    // Only when that folder does not exist yet: never overwrite an existing one.
                    extractDir = archiveDir;
                }
                else
                {
                    // Create a folder named after the archive
                    extractDir = Path.Combine(archiveDir, archiveNameWithoutExt);
                    if (Path.Exists(extractDir))
                    {
                        extractDir = Path.Combine(archiveDir, PathHelper.GenerateUniqueName(archiveDir, archiveNameWithoutExt, isDirectory: true));
                    }
                    Directory.CreateDirectory(extractDir);
                }

                var extractedFiles = new List<string>();

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.IsDirectory)
                        continue;

                    // Security: prevent path traversal. Resolve the entry against the extraction
                    // dir and require it to stay strictly inside. The trailing separator stops a
                    // sibling like "extract-evil" from matching the prefix of "extract".
                    var fullDest = Path.GetFullPath(Path.Combine(extractDir, entry.Key!));
                    var fullExtractDir = Path.GetFullPath(extractDir);
                    var extractDirPrefix = fullExtractDir.EndsWith(Path.DirectorySeparatorChar)
                        ? fullExtractDir
                        : fullExtractDir + Path.DirectorySeparatorChar;
                    if (!fullDest.StartsWith(extractDirPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "Skipped archive entry escaping the extraction directory (path traversal): {Entry} in {Archive}",
                            entry.Key, archivePath);
                        continue;
                    }

                    var destDir = Path.GetDirectoryName(fullDest);
                    if (destDir != null && !Directory.Exists(destDir))
                        Directory.CreateDirectory(destDir);

                    // Write to the already-validated path instead of WriteToDirectory, which would
                    // re-resolve entry.Key itself and bypass the check above.
                    entry.WriteToFile(fullDest, new ExtractionOptions { Overwrite = true });

                    extractedFiles.Add(fullDest);
                }

                _logger.LogInformation(
                    "Extracted {Count} file(s) from {Archive} into {Directory}",
                    extractedFiles.Count, archivePath, extractDir);

                return new ArchiveExtractResult
                {
                    IsSuccess = true,
                    ExtractedDirectory = extractDir,
                    ExtractedFiles = extractedFiles,
                    FileCount = extractedFiles.Count
                };
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Extraction cancelled: {Path}", archivePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract archive {Path}", archivePath);
            return ArchiveExtractResult.Failure($"Erreur lors de l'extraction : {ex.Message}");
        }
    }

    // Name of the only top-level folder when every entry lives under it, otherwise null.
    private static string? GetSingleRootFolder(IArchive archive)
    {
        var rootEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            var key = entry.Key?.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(key)) continue;

            var firstSegment = key.Split('/')[0];
            rootEntries.Add(firstSegment);

            if (rootEntries.Count > 1)
                return null;
        }

        // Single root and it must be a directory (has entries inside it)
        if (rootEntries.Count == 1)
        {
            var root = rootEntries.First();
            var isFolder = archive.Entries.Any(e =>
            {
                var key = e.Key?.Replace('\\', '/').TrimStart('/');
                return key != null && key.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
            });
            return isFolder ? root : null;
        }

        return null;
    }

    private static string GetArchiveNameWithoutExtension(string path)
    {
        var fileName = Path.GetFileName(path);

        // Handle .tar.gz, .tar.bz2, .tar.xz
        var doubleExtensions = new[] { ".tar.gz", ".tar.bz2", ".tar.xz" };
        foreach (var ext in doubleExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return fileName[..^ext.Length];
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }
}

public class ArchiveExtractResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ExtractedDirectory { get; init; }
    public List<string> ExtractedFiles { get; init; } = new();
    public int FileCount { get; init; }

    public static ArchiveExtractResult Failure(string message) => new() { IsSuccess = false, ErrorMessage = message };
}
