using FileIndexer.Models;
using FileIndexer.Services;

namespace FileIndexer.Mcp;

// Runs one rescan at a time in the background, so a long scan outlives the tool call that
// started it, and remembers how the last one ended.
public sealed class ScanJobs(FileScannerService scanner, IndexPaths paths)
{
    private readonly Lock _gate = new();
    private Task<ScanProgress>? _job;
    private string? _jobPath;

    public async Task<string> StartAsync(string path, TimeSpan wait)
    {
        if (await paths.ResolveAsync(path) is not { } location)
            return await paths.NotIndexedAsync(path);

        Task<ScanProgress> job;
        lock (_gate)
        {
            if (_job is { IsCompleted: false })
                return $"A scan is already running. {Describe()}";

            var collection = location.Collection;
            _jobPath = location.Path;
            _job = job = Task.Run(() => scanner.ScanCollectionAsync(
                collection.Id,
                collection.Paths.Select(p => p.Path),
                incrementalScan: true,
                collection.GetExcludedDirectoryList(),
                subtree: location.Path));
        }

        await Task.WhenAny(job, Task.Delay(wait));
        return Describe();
    }

    public string Describe()
    {
        lock (_gate)
        {
            if (_job == null) return "No scan started since the server started.";
            var p = scanner.CurrentProgress;
            return _job.Status switch
            {
                TaskStatus.RanToCompletion =>
                    $"Scan of {_jobPath} done in {p.Elapsed.TotalSeconds:F0} s: {p.FilesScanned} entries checked, {p.FilesRemoved} removed, {p.ErrorCount} errors.",
                TaskStatus.Faulted or TaskStatus.Canceled =>
                    $"Scan of {_jobPath} failed: {_job.Exception?.GetBaseException().Message ?? "cancelled"}",
                _ => $"Scan of {_jobPath} running: {p.DirectoriesScanned} folders, {p.FilesScanned} entries checked so far. Call index_status to follow it."
            };
        }
    }
}
