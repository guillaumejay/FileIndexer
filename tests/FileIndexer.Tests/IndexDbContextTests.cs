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

        await db.UpsertFilesAsync([
            MakeFile(col.Id, "animist-guide.pdf"),
            MakeFile(col.Id, "warrior.txt")
        ]);

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
        await db.UpsertFilesAsync(new[] { MakeFile(col.Id, "file.txt") });

        var result = await db.SearchAsync("+++");

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task DeletePaths_RemovesEntryAndDescendantsOnly()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        await db.UpsertFilesAsync([
            MakeFile(col.Id, "deleteme", @"C:\data"),
            MakeFile(col.Id, "inner.txt", @"C:\data\deleteme"),
            MakeFile(col.Id, "deleteme-sibling.txt", @"C:\data")
        ]);

        await db.DeletePathsAsync([@"C:\data\deleteme"]);

        // The range filter must not catch "deleteme-sibling.txt" (same prefix, no separator).
        var left = Assert.Single((await db.SearchAsync("")).Files);
        Assert.Equal("deleteme-sibling.txt", left.Name);
    }

    [Fact]
    public async Task GetFilesByIds_ReturnsRequestedFiles()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        await db.UpsertFilesAsync(new[]
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

    [Theory]
    [InlineData("NOT")]
    [InlineData("OR")]
    [InlineData("AND")]
    [InlineData("NEAR")]
    public async Task Search_Fts5Keyword_IsSearchedAsText(string keyword)
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        await db.UpsertFilesAsync([MakeFile(col.Id, $"{keyword.ToLowerInvariant()}-file.txt")]);

        var result = await db.SearchAsync(keyword);

        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task DeleteCollection_CascadesToFilesAndPaths()
    {
        // foreign_keys used to be OFF, leaving orphan rows that stayed searchable.
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null, paths: [@"C:\data"]);
        await db.UpsertFilesAsync([MakeFile(col.Id, "orphan.txt")]);

        await db.DeleteCollectionAsync(col.Id);

        Assert.Equal(0, (await db.SearchAsync("orphan")).TotalCount);
        Assert.Null(await db.GetCollectionByIdAsync(col.Id));
    }

    [Fact]
    public async Task Upsert_SamePathTwice_UpdatesSingleRow()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        var file = MakeFile(col.Id, "same.txt");
        await db.UpsertFilesAsync([file]);

        file.SizeBytes = 42;
        await db.UpsertFilesAsync([file]);

        var row = Assert.Single((await db.SearchAsync("same")).Files);
        Assert.Equal(42, row.SizeBytes);
    }

    [Fact]
    public async Task Dates_RoundTripAsUtc()
    {
        using var db = new IndexDbContext(":memory:");
        var col = await db.CreateCollectionAsync("test", null);
        var file = MakeFile(col.Id, "dated.txt");
        await db.UpsertFilesAsync([file]);

        var row = Assert.Single((await db.SearchAsync("dated")).Files);

        Assert.Equal(DateTimeKind.Utc, row.ModifiedAtUtc.Kind);
        Assert.Equal(file.ModifiedAtUtc, row.ModifiedAtUtc);
    }

    [Fact]
    public async Task GetCollections_ReturnsPathsAndStats()
    {
        using var db = new IndexDbContext(":memory:");
        var a = await db.CreateCollectionAsync("a", null, paths: [@"C:\a1", @"C:\a2"]);
        await db.CreateCollectionAsync("b", null);
        await db.UpsertFilesAsync([MakeFile(a.Id, "x.txt"), MakeFile(a.Id, "y.txt")]);

        var collections = await db.GetCollectionsAsync();

        var loadedA = collections.Single(c => c.Name == "a");
        Assert.Equal(2, loadedA.Paths.Count);
        Assert.Equal(2, loadedA.FileCount);
        Assert.NotNull(loadedA.LastIndexedAtUtc);
        Assert.Equal(0, collections.Single(c => c.Name == "b").FileCount);
    }

    [Fact]
    public async Task OpeningLegacyDatabase_RemovesDuplicatesAndOrphans()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fi-legacy-{Guid.NewGuid():N}.db");
        try
        {
            // Build a pre-migration database: no unique index, a duplicated row, an orphan row.
            using (var legacy = new IndexDbContext(path)) { }
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    PRAGMA foreign_keys=OFF;
                    DROP INDEX ux_files_collection_path;
                    INSERT INTO collections (id, name, created_at_utc) VALUES (1, 'c', '2026-01-01T00:00:00.0000000Z');
                    INSERT INTO files (collection_id, name, path, directory, extension, size_bytes, created_at_utc, modified_at_utc, indexed_at_utc)
                    VALUES (1, 'dup.txt', 'C:\d\dup.txt', 'C:\d', '.txt', 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                           (1, 'dup.txt', 'C:\d\dup.txt', 'C:\d', '.txt', 2, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                           (7, 'orphan.txt', 'C:\d\orphan.txt', 'C:\d', '.txt', 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
                    """;
                cmd.ExecuteNonQuery();
            }

            using var db = new IndexDbContext(path);

            var all = await db.SearchAsync("");
            var row = Assert.Single(all.Files);
            Assert.Equal(2, row.SizeBytes); // most recent duplicate kept
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { path, path + "-wal", path + "-shm" })
                try { File.Delete(f); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task ConcurrentReadsAndWrites_DoNotThrow()
    {
        // Regression test for issue #1: readers run in parallel while a writer inserts. With a
        // single shared connection this threw / corrupted; with connection-per-operation + WAL
        // it must be safe.
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
                await db.BulkUpsertAsync(files);
            }));
            tasks.Add(Task.Run(async () =>
            {
                for (var n = 0; n < 50; n++)
                {
                    await db.GetCollectionFileStampsAsync(col.Id);
                }
            }));
        }

        // Must complete without InvalidOperationException / "database is locked".
        await Task.WhenAll(tasks);

        var stats = await db.GetStatsAsync();
        Assert.Equal(16 * 50, stats.TotalFiles);
    }
}
