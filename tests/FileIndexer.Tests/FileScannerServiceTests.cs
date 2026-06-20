using FileIndexer.Data;
using FileIndexer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileIndexer.Tests;

// Drives FileScannerService against a real temp directory tree and an in-memory index.
// Covers full vs incremental scans, directory-name exclusions, and the guard/edge paths.
public class FileScannerServiceTests : IDisposable
{
    private readonly string _root;
    private readonly IndexDbContext _db;
    private int _collectionId;

    public FileScannerServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fi-scanner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _db = new IndexDbContext(":memory:");
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // --- Helpers -----------------------------------------------------------------------------

    private FileScannerService NewScanner() => new(_db, NullLogger<FileScannerService>.Instance);

    private async Task InitCollection()
    {
        var col = await _db.CreateCollectionAsync("test", null);
        _collectionId = col.Id;
    }

    // Writes a file at a path relative to the temp root, creating intermediate dirs.
    private string Write(string relativePath, string content = "x")
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    // Number of indexed (non-directory) files whose name matches the FTS query.
    private async Task<int> CountFiles(string query)
    {
        var result = await _db.SearchAsync(query);
        return result.Files.Count(f => !f.IsDirectory);
    }

    // --- Full scan ---------------------------------------------------------------------------

    [Fact]
    public async Task FullScan_IndexesAllFilesIncludingSubdirectories()
    {
        await InitCollection();
        Write("alpha.txt");
        Write("bravo.txt");
        Write(Path.Combine("nested", "charlie.txt"));

        var progress = await NewScanner().ScanCollectionAsync(_collectionId, new[] { _root });

        Assert.True(progress.IsComplete);
        Assert.False(progress.IsRunning);
        Assert.Equal(1, await CountFiles("alpha"));
        Assert.Equal(1, await CountFiles("bravo"));
        Assert.Equal(1, await CountFiles("charlie"));
    }

    [Fact]
    public async Task FullScan_NonIncremental_ClearsPreviousEntries()
    {
        await InitCollection();
        Write("alpha.txt");
        await NewScanner().ScanCollectionAsync(_collectionId, new[] { _root });
        Assert.Equal(1, await CountFiles("alpha"));

        // Replace the content of the collection on disk and rescan from scratch.
        File.Delete(Path.Combine(_root, "alpha.txt"));
        Write("beta.txt");

        await NewScanner().ScanCollectionAsync(_collectionId, new[] { _root });

        Assert.Equal(0, await CountFiles("alpha")); // cleared and gone from disk
        Assert.Equal(1, await CountFiles("beta"));
    }

    // --- Exclusions --------------------------------------------------------------------------

    [Fact]
    public async Task Scan_ExcludedDirectory_IsSkipped()
    {
        await InitCollection();
        Write("rootfile.txt");
        Write(Path.Combine("keep", "kept.txt"));
        Write(Path.Combine("node_modules", "ignored.txt"));

        await NewScanner().ScanCollectionAsync(
            _collectionId, new[] { _root }, excludedDirectories: new[] { "node_modules" });

        Assert.Equal(1, await CountFiles("rootfile"));
        Assert.Equal(1, await CountFiles("kept"));
        Assert.Equal(0, await CountFiles("ignored")); // inside the excluded directory
    }

    // --- Incremental scan --------------------------------------------------------------------

    [Fact]
    public async Task IncrementalScan_SkipsUnchangedAndAddsNewFiles()
    {
        await InitCollection();
        Write("alpha.txt");
        Write("bravo.txt");
        await NewScanner().ScanCollectionAsync(_collectionId, new[] { _root });

        // Add a new file, then rescan incrementally.
        Write("charlie.txt");
        var progress = await NewScanner().ScanCollectionAsync(
            _collectionId, new[] { _root }, incrementalScan: true);

        Assert.True(progress.IsComplete);
        Assert.Equal(1, await CountFiles("charlie")); // newly indexed
        // Unchanged files were skipped, not duplicated.
        Assert.Equal(1, await CountFiles("alpha"));
        Assert.Equal(1, await CountFiles("bravo"));
    }

    // --- Guards / edge cases -----------------------------------------------------------------

    [Fact]
    public async Task Scan_NoPaths_Throws()
    {
        await InitCollection();
        var scanner = NewScanner();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scanner.ScanCollectionAsync(_collectionId, Array.Empty<string>()));
    }

    [Fact]
    public async Task Scan_NonExistentPath_CompletesWithoutIndexing()
    {
        await InitCollection();
        var missing = Path.Combine(_root, "does-not-exist");

        var progress = await NewScanner().ScanCollectionAsync(_collectionId, new[] { missing });

        Assert.True(progress.IsComplete);
        Assert.Equal(0, (await _db.SearchAsync("")).TotalCount);
    }
}
