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

    public async Task<ScanProgress> ScanAsync(string rootPath, bool incrementalScan = false)
    {
        if (_progress.IsRunning)
        {
            throw new InvalidOperationException("Un scan est déjà en cours");
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        
        _progress = new ScanProgress { IsRunning = true };
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Démarrage du scan: {Path}, Incrémental: {Incremental}", rootPath, incrementalScan);

            if (!incrementalScan)
            {
                await _db.ClearAsync();
            }

            // Phase 1: Énumérer tous les répertoires d'abord (rapide)
            var directories = await EnumerateDirectoriesAsync(rootPath, ct);
            _progress.DirectoriesScanned = directories.Count;
            
            // Estimer le nombre de fichiers (heuristique)
            _progress.FilesTotal = directories.Count * 50; // Estimation initiale
            NotifyProgress();

            // Phase 2: Scanner les fichiers en parallèle avec Channel pour back-pressure
            var fileChannel = Channel.CreateBounded<IndexedFile>(new BoundedChannelOptions(BatchSize * 2)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

            // Producer: Scanner les fichiers
            var producerTask = ProduceFilesAsync(directories, fileChannel.Writer, incrementalScan, ct);
            
            // Consumer: Écrire en base par batches
            var consumerTask = ConsumeFilesAsync(fileChannel.Reader, ct);

            await Task.WhenAll(producerTask, consumerTask);

            sw.Stop();
            _progress.Elapsed = sw.Elapsed;
            _progress.IsRunning = false;
            _progress.IsComplete = true;
            NotifyProgress();

            _logger.LogInformation("Scan terminé: {Files} fichiers en {Time}", 
                _progress.FilesScanned, _progress.Elapsed);

            return _progress;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Scan annulé");
            _progress.IsRunning = false;
            NotifyProgress();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur pendant le scan");
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
                    _logger.LogWarning("Impossible d'accéder à {Dir}: {Error}", directory, ex.Message);
                    Interlocked.Increment(ref errors);
                    return;
                }

                foreach (var filePath in files)
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        
                        // Skip si incrémental et fichier déjà indexé avec même date
                        if (incrementalScan && await _db.FileExistsAsync(filePath, fileInfo.LastWriteTimeUtc))
                        {
                            continue;
                        }

                        var indexedFile = new IndexedFile
                        {
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
                        _logger.LogWarning("Erreur fichier {File}: {Error}", filePath, ex.Message);
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
