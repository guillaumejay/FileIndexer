using FileIndexer.Data;
using FileIndexer.Models;
using FileIndexer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileIndexer.Tests;

// Exercises FileOperationsService against a real temp filesystem and an in-memory index, with a
// fake trash service. Focus: batch operations must continue past per-file failures and aggregate
// them (issue #7) instead of aborting the whole lot on the first error.
public class FileOperationsServiceTests : IDisposable
{
    private readonly string _root;
    private readonly IndexDbContext _db;
    private readonly FakeTrashService _trash;
    private int _collectionId;

    public FileOperationsServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fileindexer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _db = new IndexDbContext(":memory:");
        _trash = new FakeTrashService();
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    // --- Conflict callbacks ------------------------------------------------------------------

    private static readonly Func<string, string, Task<ConflictResolution>> NeverConflict =
        (_, _) => Task.FromResult(ConflictResolution.KeepBoth);

    private static Func<string, string, Task<ConflictResolution>> Always(ConflictResolution r) =>
        (_, _) => Task.FromResult(r);

    // --- Helpers -----------------------------------------------------------------------------

    private FileOperationsService NewService() => new(_db, _trash, NullLogger<FileOperationsService>.Instance);

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteFile(string directory, string name, string content = "x")
    {
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task InitCollection()
    {
        var col = await _db.CreateCollectionAsync("test", null);
        _collectionId = col.Id;
    }

    // Index the given on-disk paths and return their ids (preserving the input order).
    private async Task<List<long>> Index(params string[] paths)
    {
        var records = paths.Select(p =>
        {
            var fi = new FileInfo(p);
            return new IndexedFile
            {
                CollectionId = _collectionId,
                Name = Path.GetFileName(p),
                Path = p,
                Directory = Path.GetDirectoryName(p)!,
                Extension = Path.GetExtension(p),
                SizeBytes = fi.Exists ? fi.Length : 0,
                CreatedAtUtc = DateTime.UtcNow,
                ModifiedAtUtc = DateTime.UtcNow,
                IndexedAtUtc = DateTime.UtcNow
            };
        }).ToList();

        await _db.InsertFilesAsync(records);

        var all = await _db.SearchAsync("");
        return paths.Select(p => all.Files.First(f => f.Path == p).Id).ToList();
    }

    private async Task<List<IndexedFile>> AllIndexed()
    {
        var result = await _db.SearchAsync("");
        return result.Files.ToList();
    }

    // --- Copy --------------------------------------------------------------------------------

    [Fact]
    public async Task Copy_CopiesFilesToDestinationAndIndexesThem()
    {
        await InitCollection();
        var src = Dir("src");
        var dest = Dir("dest");
        var ids = await Index(WriteFile(src, "a.txt"), WriteFile(src, "b.txt"));

        var result = await NewService().CopyFilesAsync(ids, dest, NeverConflict);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
        Assert.True(File.Exists(Path.Combine(dest, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dest, "b.txt")));
        // Originals plus the two copies are indexed.
        Assert.Equal(4, (await AllIndexed()).Count);
    }

    [Fact]
    public async Task Copy_MissingSource_IsSkippedNotFailed()
    {
        await InitCollection();
        var src = Dir("src");
        var dest = Dir("dest");
        var present = WriteFile(src, "present.txt");
        var ghost = Path.Combine(src, "ghost.txt"); // indexed but never written to disk
        var ids = await Index(present, ghost);

        var result = await NewService().CopyFilesAsync(ids, dest, NeverConflict);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.True(File.Exists(Path.Combine(dest, "present.txt")));
        Assert.False(File.Exists(Path.Combine(dest, "ghost.txt")));
    }

    [Fact]
    public async Task Copy_DestinationMissing_Fails()
    {
        await InitCollection();
        var src = Dir("src");
        var ids = await Index(WriteFile(src, "a.txt"));

        var result = await NewService().CopyFilesAsync(ids, Path.Combine(_root, "nope"), NeverConflict);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Copy_CancelledOnConflict_PersistsFilesCopiedBeforeCancel()
    {
        // Regression: cancelling mid-batch must still index whatever was physically copied,
        // otherwise on-disk copies become orphans unknown to the index.
        await InitCollection();
        var src = Dir("src");
        var dest = Dir("dest");
        WriteFile(dest, "clash.txt"); // pre-existing -> triggers the conflict prompt
        var ids = await Index(WriteFile(src, "keep.txt"), WriteFile(src, "clash.txt"));

        var result = await NewService().CopyFilesAsync(ids, dest, Always(ConflictResolution.Cancel));

        Assert.True(result.IsCancelled);

        // Invariant (order-independent): every NEW file in dest must have a matching index entry.
        var newDestFiles = Directory.GetFiles(dest)
            .Select(Path.GetFileName)
            .Where(n => n != "clash.txt") // the seeded conflict file was never copied
            .ToList();
        var indexedInDest = (await AllIndexed()).Count(f => f.Directory == dest);
        Assert.Equal(newDestFiles.Count, indexedInDest);
    }

    // --- Move --------------------------------------------------------------------------------

    [Fact]
    public async Task Move_MovesFilesAndUpdatesIndexPath()
    {
        await InitCollection();
        var src = Dir("src");
        var dest = Dir("dest");
        var ids = await Index(WriteFile(src, "a.txt"));

        var result = await NewService().MoveFilesAsync(ids, dest, NeverConflict);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.SuccessCount);
        Assert.False(File.Exists(Path.Combine(src, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dest, "a.txt")));
        var moved = Assert.Single(await AllIndexed());
        Assert.Equal(Path.Combine(dest, "a.txt"), moved.Path);
        Assert.Equal(dest, moved.Directory);
    }

    [Fact]
    public async Task Move_MissingSource_IsSkipped()
    {
        await InitCollection();
        var src = Dir("src");
        var dest = Dir("dest");
        var ghost = Path.Combine(src, "ghost.txt");
        var ids = await Index(ghost);

        var result = await NewService().MoveFilesAsync(ids, dest, NeverConflict);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.SkippedCount);
    }

    // --- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_MovesToTrashAndRemovesFromIndex()
    {
        await InitCollection();
        var src = Dir("src");
        var ids = await Index(WriteFile(src, "a.txt"), WriteFile(src, "b.txt"));

        var result = await NewService().DeleteFilesAsync(ids);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(2, _trash.Trashed.Count);
        Assert.Empty(await AllIndexed());
    }

    [Fact]
    public async Task Delete_TrashFailureOnOneFile_ContinuesAndAggregates()
    {
        await InitCollection();
        var src = Dir("src");
        var good = WriteFile(src, "good.txt");
        var bad = WriteFile(src, "bad.txt");
        var ids = await Index(good, bad);

        // Trash rejects exactly one file; the batch must still process the other.
        _trash.FailFor = path => path == bad;

        var result = await NewService().DeleteFilesAsync(ids);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(bad, result.Errors.Single().Path);
        // The good file was trashed and dropped from the index; the failed one remains.
        var remaining = Assert.Single(await AllIndexed());
        Assert.Equal(bad, remaining.Path);
    }

    [Fact]
    public async Task Delete_FileAlreadyGoneFromDisk_RemovedFromIndexAsSkipped()
    {
        await InitCollection();
        var src = Dir("src");
        var ghost = Path.Combine(src, "ghost.txt"); // indexed but absent on disk
        var ids = await Index(ghost);

        var result = await NewService().DeleteFilesAsync(ids);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.SuccessCount);
        Assert.Empty(_trash.Trashed);
        Assert.Empty(await AllIndexed());
    }

    [Fact]
    public async Task Delete_NoMatchingFiles_Fails()
    {
        await InitCollection();

        var result = await NewService().DeleteFilesAsync(new long[] { 999 });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    // --- Fake trash service ------------------------------------------------------------------

    private sealed class FakeTrashService : ITrashService
    {
        public List<string> Trashed { get; } = new();
        public Func<string, bool>? FailFor { get; set; }
        public bool IsSupported => true;

        public Task<OperationResult> MoveToTrashAsync(string path)
        {
            if (FailFor?.Invoke(path) == true)
            {
                return Task.FromResult(OperationResult.Failure($"trash refused {path}"));
            }
            Trashed.Add(path);
            return Task.FromResult(OperationResult.Success());
        }
    }
}
