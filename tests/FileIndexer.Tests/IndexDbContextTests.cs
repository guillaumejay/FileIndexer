using FileIndexer.Data;
using FileIndexer.Models;

namespace FileIndexer.Tests;

// Tests run against an in-memory database (the shared-cache + keep-alive path), which also
// exercises the connection-per-operation model introduced for issue #1.
public class IndexDbContextTests
{
    private static IndexedFile MakeFile(int collectionId, string name, string dir = @"C:\data")
    {
        var now = DateTime.UtcNow;
        return new IndexedFile
        {
            CollectionId = collectionId,
            Name = name,
            Path = System.IO.Path.Combine(dir, name),
            Directory = dir,
            Extension = System.IO.Path.GetExtension(name),
            SizeBytes = 10,
            IsDirectory = false,
            CreatedAtUtc = now,
            ModifiedAtUtc = now,
            IndexedAtUtc = now
        };
    }

    [Fact]
    public async Task InsertAndSearch_FindsFileByPrefix()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);

        await db.InsertFilesAsync(new[]
        {
            MakeFile(col.Id, "animist-guide.pdf"),
            MakeFile(col.Id, "warrior.txt")
        });

        var result = await db.SearchAsync("anim");

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("animist-guide.pdf", Assert.Single(result.Files).Name);
    }

    [Fact]
    public async Task Search_PunctuationOnlyQuery_ReturnsEmptyWithoutThrowing()
    {
        // Regression test for issue #2: an FTS MATCH '' would otherwise throw a SQLite syntax error.
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        await db.InsertFilesAsync(new[] { MakeFile(col.Id, "file.txt") });

        var result = await db.SearchAsync("+++");

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task DeleteFilesByIds_RemovesFromIndexAndSearch()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        await db.InsertFilesAsync(new[] { MakeFile(col.Id, "deleteme.txt") });

        var inserted = await db.SearchAsync("deleteme");
        var id = inserted.Files.Single().Id;

        await db.DeleteFilesByIdsAsync(new[] { id });

        var after = await db.SearchAsync("deleteme");
        Assert.Equal(0, after.TotalCount);
    }

    [Fact]
    public async Task GetFilesByIds_ReturnsRequestedFiles()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        await db.InsertFilesAsync(new[]
        {
            MakeFile(col.Id, "a.txt"),
            MakeFile(col.Id, "b.txt"),
            MakeFile(col.Id, "c.txt")
        });

        var all = await db.SearchAsync("");
        var ids = all.Files.Take(2).Select(f => f.Id).ToList();

        var fetched = await db.GetFilesByIdsAsync(ids);
        Assert.Equal(2, fetched.Count);
        Assert.All(fetched, f => Assert.Contains(f.Id, ids));
    }

    [Fact]
    public async Task ConcurrentReadsAndWrites_DoNotThrow()
    {
        // Regression test for issue #1: the scanner reads (FileExists*) in parallel while a
        // writer inserts. With a single shared connection this threw / corrupted; with
        // connection-per-operation + WAL it must be safe.
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);

        var tasks = new List<Task>();
        for (var i = 0; i < 16; i++)
        {
            var batch = i;
            tasks.Add(Task.Run(async () =>
            {
                var files = Enumerable.Range(0, 50)
                    .Select(n => MakeFile(col.Id, $"file_{batch}_{n}.txt"))
                    .ToList();
                await db.BulkInsertAsync(files);
            }));
            tasks.Add(Task.Run(async () =>
            {
                for (var n = 0; n < 50; n++)
                {
                    await db.FileExistsInCollectionAsync($@"C:\data\file_{batch}_{n}.txt", col.Id, DateTime.UtcNow);
                }
            }));
        }

        // Must complete without InvalidOperationException / "database is locked".
        await Task.WhenAll(tasks);

        var stats = await db.GetStatsAsync();
        Assert.Equal(16 * 50, stats.TotalFiles);
    }
}
