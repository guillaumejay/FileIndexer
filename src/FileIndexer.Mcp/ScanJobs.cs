using FileIndexer.Models;
using FileIndexer.Services;

namespace FileIndexer.Mcp;

// Runs rescans in the background, so a long scan outlives the tool call that started it, and
// remembers how the last one ended. The scanner itself allows one scan at a time.
public sealed class ScanJobs(FileScannerService scanner, IndexPaths paths)
{
    private sealed record Job(Task<ScanProgress> Scan, string Path);

    private readonly Lock _gate = new();
    private volatile Job? _last;

    public async Task<string> StartAsync(string path, TimeSpan wait)
    {
        var location = await paths.ResolveAsync(path);
        Job job;
        lock (_gate)
        {
            // The job check covers a scan queued here that has not reached the scanner yet.
            if (scanner.IsRunning || _last is { Scan.IsCompleted: false })
                return $"A scan is already running. {Describe()}";
            // Task.Run: SQLite's async calls complete synchronously, so loading the index
            // stamps would otherwise block this tool call.
            _last = job = new Job(
                Task.Run(() => scanner.ScanCollectionAsync(location.Collection, incrementalScan: true, subtree: location.Path)),
                location.Path);
        }

        await Task.WhenAny(job.Scan, Task.Delay(wait));
        return Describe();
    }

    public string Describe()
    {
        if (_last is not { } job) return "No scan started since the server started.";
        var p = scanner.CurrentProgress;
        return job.Scan.Status switch
        {
            TaskStatus.RanToCompletion =>
                $"Scan of {job.Path} done in {p.Elapsed.TotalSeconds:F0} s: {p.FilesScanned} entries checked, {p.FilesRemoved} removed, {p.ErrorCount} errors.",
            TaskStatus.Faulted or TaskStatus.Canceled =>
                $"Scan of {job.Path} failed: {job.Scan.Exception?.GetBaseException().Message ?? "cancelled"}",
            _ => $"Scan of {job.Path} running: {p.DirectoriesScanned} folders, {p.FilesScanned} entries checked so far. Call index_status to follow it."
        };
    }
}
