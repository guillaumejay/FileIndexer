using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FileIndexer.Models;
using FileIndexer.Data;

namespace FileIndexer.Services;

public class FileScannerService
{
    private readonly IndexDbContext _db;
    private readonly ILogger<FileScannerService> _logger;
    private CancellationTokenSource? _cts;
    private ScanProgress _progress = new();
    
    public event Action<ScanProgress>? OnProgressChanged;
    
    // Configuration
    public int DegreeOfParallelism { get; set; } = 64; // Ajustable selon le NAS
    public int BatchSize { get; set; } = 500;

    public FileScannerService(IndexDbContext db, ILogger<FileScannerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ScanProgress CurrentProgress => _progress;

    public async Task<ScanProgress> ScanCollectionAsync(int collectionId, IEnumerable<string> rootPaths, bool incrementalScan = false)
    {
        if (_progress.IsRunning)
        {
            throw new InvalidOperationException("A scan is already running");
        }

        var pathList = rootPaths.ToList();
        if (pathList.Count == 0)
        {
            throw new InvalidOperationException("No paths configured for this collection");
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _progress = new ScanProgress { IsRunning = true };
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting scan for collection {CollectionId} with {PathCount} paths, Incremental: {Incremental}",
                collectionId, pathList.Count, incrementalScan);

            if (!incrementalScan)
            {
                await _db.ClearCollectionAsync(collectionId);
            }

            // Phase 1: Enumerate all directories from all root paths
            var allDirectories = new List<string>();
            foreach (var rootPath in pathList)
            {
                if (Directory.Exists(rootPath))
                {
                    var dirs = await EnumerateDirectoriesAsync(rootPath, ct);
                    allDirectories.AddRange(dirs);
                }
                else
                {
                    _logger.LogWarning("Path does not exist: {Path}", rootPath);
                }
            }

            _progress.DirectoriesScanned = allDirectories.Count;
            _progress.FilesTotal = allDirectories.Count * 50; // Initial estimate
            NotifyProgress();

            // Phase 2: Scan files in parallel with Channel for back-pressure
            var fileChannel = Channel.CreateBounded<IndexedFile>(new BoundedChannelOptions(BatchSize * 2)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            // Producer: Scan files and tag with collectionId
            var producerTask = ProduceFilesAsync(allDirectories, fileChannel.Writer, collectionId, incrementalScan, ct);

            // Consumer: Write to DB in batches
            var consumerTask = ConsumeFilesAsync(fileChannel.Reader, ct);

            await Task.WhenAll(producerTask, consumerTask);

            sw.Stop();
            _progress.Elapsed = sw.Elapsed;
            _progress.IsRunning = false;
            _progress.IsComplete = true;
            NotifyProgress();

            _logger.LogInformation("Scan complete: {Files} files in {Time}",
                _progress.FilesScanned, _progress.Elapsed);

            return _progress;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Scan cancelled");
            _progress.IsRunning = false;
            NotifyProgress();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scan");
            _progress.IsRunning = false;
            _progress.ErrorCount++;
            NotifyProgress();
            throw;
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task<List<string>> EnumerateDirectoriesAsync(string rootPath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var dirs = new List<string> { rootPath };
            
            try
            {
                var enumerated = Directory.EnumerateDirectories(rootPath, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System
                });

                foreach (var dir in enumerated)
                {
                    ct.ThrowIfCancellationRequested();
                    dirs.Add(dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur énumération répertoires");
            }

            return dirs;
        }, ct);
    }

    private async Task ProduceFilesAsync(
        List<string> directories,
        ChannelWriter<IndexedFile> writer,
        int collectionId,
        bool incrementalScan,
        CancellationToken ct)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = DegreeOfParallelism,
            CancellationToken = ct
        };

        var filesScanned = 0;
        var errors = 0;

        try
        {
            await Parallel.ForEachAsync(directories, options, async (directory, token) =>
            {
                _progress.CurrentDirectory = directory;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(directory);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Cannot access {Dir}: {Error}", directory, ex.Message);
                    Interlocked.Increment(ref errors);
                    return;
                }

                foreach (var filePath in files)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(filePath);

                        // Skip if incremental and file already indexed with same date
                        if (incrementalScan && await _db.FileExistsInCollectionAsync(filePath, collectionId, fileInfo.LastWriteTimeUtc))
                        {
                            continue;
                        }

                        var indexedFile = new IndexedFile
                        {
                            CollectionId = collectionId,
                            Name = fileInfo.Name,
                            Path = fileInfo.FullName,
                            Directory = fileInfo.DirectoryName ?? "",
                            Extension = fileInfo.Extension.ToLowerInvariant(),
                            SizeBytes = fileInfo.Length,
                            CreatedAtUtc = fileInfo.CreationTimeUtc,
                            ModifiedAtUtc = fileInfo.LastWriteTimeUtc,
                            IndexedAtUtc = DateTime.UtcNow
                        };

                        await writer.WriteAsync(indexedFile, token);

                        var current = Interlocked.Increment(ref filesScanned);
                        if (current % 100 == 0)
                        {
                            _progress.FilesScanned = current;
                            _progress.FilesTotal = Math.Max(_progress.FilesTotal, current + 1000);
                            _progress.ErrorCount = errors;
                            NotifyProgress();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error on file {File}: {Error}", filePath, ex.Message);
                        Interlocked.Increment(ref errors);
                    }
                }
            });
        }
        finally
        {
            _progress.FilesScanned = filesScanned;
            _progress.FilesTotal = filesScanned;
            _progress.ErrorCount = errors;
            writer.Complete();
        }
    }

    private async Task ConsumeFilesAsync(ChannelReader<IndexedFile> reader, CancellationToken ct)
    {
        var batch = new List<IndexedFile>(BatchSize);

        await foreach (var file in reader.ReadAllAsync(ct))
        {
            batch.Add(file);

            if (batch.Count >= BatchSize)
            {
                await _db.BulkInsertAsync(batch);
                batch.Clear();
            }
        }

        // Insérer le reste
        if (batch.Count > 0)
        {
            await _db.BulkInsertAsync(batch);
        }
    }

    private void NotifyProgress()
    {
        OnProgressChanged?.Invoke(_progress);
    }
}
