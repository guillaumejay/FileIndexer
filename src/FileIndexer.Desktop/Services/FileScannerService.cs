using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using FileIndexer.Models;
using FileIndexer.Data;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Services;

public class FileScannerService
{
    private readonly IndexDbContext _db;
    private readonly ILogger<FileScannerService> _logger;
    private CancellationTokenSource? _cts;
    private ScanProgress _progress = new();
    private int _running;

    public event Action<ScanProgress>? OnProgressChanged;

    // Configuration
    public int DegreeOfParallelism { get; set; } = 64; // Tune for the NAS
    public int BatchSize { get; set; } = 500;

    private const int ProgressIntervalMs = 250;

    public FileScannerService(IndexDbContext db, ILogger<FileScannerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public ScanProgress CurrentProgress => _progress;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    // Scans the collection roots and brings the index in line with the disk: new and changed
    // entries are upserted, entries no longer found are removed once the scan completes.
    // The collection is never wiped up front, so search keeps working during a rescan and a
    // cancelled or failed scan leaves the previous index intact.
    // incrementalScan only skips rewriting entries whose modification date is unchanged.
    public async Task<ScanProgress> ScanCollectionAsync(int collectionId, IEnumerable<string> rootPaths, bool incrementalScan = false, IEnumerable<string>? excludedDirectories = null)
    {
        var pathList = rootPaths.ToList();
        if (pathList.Count == 0)
        {
            throw new InvalidOperationException("No paths configured for this collection");
        }

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            throw new InvalidOperationException("A scan is already running");
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _progress = new ScanProgress { IsRunning = true, CollectionId = collectionId };
        NotifyProgress();
        var sw = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting scan for collection {CollectionId} with {PathCount} paths, Incremental: {Incremental}",
                collectionId, pathList.Count, incrementalScan);

            var excludedDirSet = new HashSet<string>(
                excludedDirectories ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            // Path -> modified ticks of what the index holds. Entries are removed as they are found
            // on disk, so whatever is left at the end is no longer there.
            var notSeenYet = new ConcurrentDictionary<string, long>(
                await _db.GetCollectionFileStampsAsync(collectionId), StringComparer.Ordinal);
            // Roots that are offline and folders that could not be listed: what the index holds
            // below them is kept rather than purged, since we could not check it.
            var unverified = new ConcurrentBag<string>();

            // Phase 1: Enumerate all directories from all root paths
            var allDirectories = new List<string>();
            foreach (var rootPath in pathList)
            {
                if (Directory.Exists(rootPath))
                {
                    allDirectories.AddRange(await Task.Run(() => EnumerateDirectories(rootPath, excludedDirSet, unverified, ct), ct));
                }
                else
                {
                    _logger.LogWarning("Path does not exist: {Path}", rootPath);
                    unverified.Add(rootPath);
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

            // If the writer fails, producers would block forever on the full channel: the linked
            // token lets the consumer stop them.
            using var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var consumerTask = ConsumeFilesAsync(fileChannel.Reader, pipelineCts);
            var producerTask = ProduceFilesAsync(
                allDirectories, fileChannel.Writer, collectionId, incrementalScan, notSeenYet, unverified, pipelineCts.Token);

            // Consumer first so its exception (the root cause) is the one rethrown.
            await Task.WhenAll(consumerTask, producerTask);
            ct.ThrowIfCancellationRequested();

            // Phase 3: drop entries that are no longer on disk (or are now excluded / outside the roots).
            IEnumerable<string> stale = notSeenYet.Keys;
            if (!unverified.IsEmpty)
            {
                var unverifiedRoots = unverified.ToList();
                stale = stale.Where(p => !unverifiedRoots.Any(root => PathHelper.IsSameOrUnder(p, root)));
            }
            stale = stale.ToList();
            _progress.FilesRemoved = stale.Any() ? await _db.DeleteCollectionFilesByPathsAsync(collectionId, stale) : 0;

            sw.Stop();
            _progress.Elapsed = sw.Elapsed;
            _progress.IsRunning = false;
            _progress.IsComplete = true;
            NotifyProgress();

            _logger.LogInformation("Scan complete: {Files} entries seen, {Removed} removed in {Time}",
                _progress.FilesScanned, _progress.FilesRemoved, _progress.Elapsed);

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
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    // Walks the tree manually so an excluded folder prunes its whole subtree, not just itself.
    private List<string> EnumerateDirectories(string rootPath, HashSet<string> excludedDirNames, ConcurrentBag<string> unverified, CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        var dirs = new List<string>();
        var pending = new Stack<string>();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();
            dirs.Add(dir);

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir, "*", options))
                {
                    if (!excludedDirNames.Contains(Path.GetFileName(sub)))
                        pending.Push(sub);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Cannot list subdirectories of {Dir}: {Error}", dir, ex.Message);
                unverified.Add(dir);
            }
        }

        return dirs;
    }

    private async Task ProduceFilesAsync(
        List<string> directories,
        ChannelWriter<IndexedFile> writer,
        int collectionId,
        bool incrementalScan,
        ConcurrentDictionary<string, long> notSeenYet,
        ConcurrentBag<string> unverified,
        CancellationToken ct)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = DegreeOfParallelism,
            CancellationToken = ct
        };

        var filesScanned = 0;
        var errors = 0;
        long nextProgressAt = 0;

        // Records the entry as present on disk and queues it unless the incremental scan
        // already has it with the same modification date.
        async ValueTask IndexEntryAsync(FileSystemInfo info, CancellationToken token)
        {
            var wasIndexed = notSeenYet.TryRemove(info.FullName, out var indexedTicks);
            if (!(incrementalScan && wasIndexed && indexedTicks == info.LastWriteTimeUtc.Ticks))
            {
                await writer.WriteAsync(IndexedFile.FromFileSystemInfo(info, collectionId), token);
            }

            var current = Interlocked.Increment(ref filesScanned);
            // Progress goes to every UI session: report a few times a second, not per file count.
            var now = Environment.TickCount64;
            var due = Interlocked.Read(ref nextProgressAt);
            if (now >= due && Interlocked.CompareExchange(ref nextProgressAt, now + ProgressIntervalMs, due) == due)
            {
                _progress.FilesScanned = current;
                _progress.FilesTotal = Math.Max(_progress.FilesTotal, current + 1000);
                _progress.ErrorCount = errors;
                NotifyProgress();
            }
        }

        try
        {
            await Parallel.ForEachAsync(directories, options, async (directory, token) =>
            {
                _progress.CurrentDirectory = directory;
                var dirInfo = new DirectoryInfo(directory);

                try
                {
                    await IndexEntryAsync(dirInfo, token);

                    foreach (var fileInfo in dirInfo.EnumerateFiles())
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            await IndexEntryAsync(fileInfo, token);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // e.g. the file vanished between listing and reading its metadata
                            _logger.LogWarning("Error on file {File}: {Error}", fileInfo.FullName, ex.Message);
                            Interlocked.Increment(ref errors);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning("Cannot access {Dir}: {Error}", directory, ex.Message);
                    unverified.Add(directory);
                    Interlocked.Increment(ref errors);
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

    // Drains the channel until the producer completes it (also on cancellation, so the batch in
    // flight is still written). On a write failure, cancels the producers so they don't block.
    private async Task ConsumeFilesAsync(ChannelReader<IndexedFile> reader, CancellationTokenSource pipelineCts)
    {
        try
        {
            var batch = new List<IndexedFile>(BatchSize);
            await foreach (var file in reader.ReadAllAsync())
            {
                batch.Add(file);
                if (batch.Count >= BatchSize)
                {
                    await _db.UpsertFilesAsync(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await _db.UpsertFilesAsync(batch);
            }
        }
        catch
        {
            await pipelineCts.CancelAsync();
            throw;
        }
    }

    private void NotifyProgress()
    {
        OnProgressChanged?.Invoke(_progress);
    }
}
