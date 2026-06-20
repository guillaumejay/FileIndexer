using FileIndexer.Data;
using FileIndexer.Models;
using FileIndexer.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace FileIndexer.Tests;

// SearchService is a thin facade over IndexDbContext, so these tests exercise the real query
// behaviour through it: collection filtering, sorting, extension/directory filters, the
// directory toggle, extension normalization, stats and pagination (i.e. beyond BuildFtsQuery).
public class SearchServiceTests : IDisposable
{
    private readonly IndexDbContext _db;
    private readonly SearchService _search;

    public SearchServiceTests()
    {
        _db = new IndexDbContext(":memory:");
        _search = new SearchService(_db, NullLogger<SearchService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // --- Helpers -----------------------------------------------------------------------------

    private async Task<int> NewCollection(string name) => (await _db.CreateCollectionAsync(name, null)).Id;

    private static IndexedFile File(
        int collectionId, string name, string dir = @"C:\data",
        long size = 10, bool isDirectory = false, DateTime? modified = null)
    {
        var ts = modified ?? DateTime.UtcNow;
        return new IndexedFile
        {
            CollectionId = collectionId,
            Name = name,
            Path = System.IO.Path.Combine(dir, name),
            Directory = dir,
            Extension = System.IO.Path.GetExtension(name),
            SizeBytes = size,
            IsDirectory = isDirectory,
            CreatedAtUtc = ts,
            ModifiedAtUtc = ts,
            IndexedAtUtc = ts
        };
    }

    private Task Insert(params IndexedFile[] files) => _db.InsertFilesAsync(files);

    // --- Collection filter -------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_FiltersByCollection()
    {
        var colA = await NewCollection("A");
        var colB = await NewCollection("B");
        await Insert(
            File(colA, "shared.txt", dir: @"C:\a"),
            File(colB, "shared.txt", dir: @"C:\b"));

        var all = await _search.SearchAsync("shared");
        var onlyA = await _search.SearchAsync("shared", collectionIds: new[] { colA });

        Assert.Equal(2, all.TotalCount);
        Assert.Equal(1, onlyA.TotalCount);
        Assert.Equal(@"C:\a", Assert.Single(onlyA.Files).Directory);
    }

    // --- Sorting -----------------------------------------------------------------------------

    [Fact]
    public async Task SearchWithSort_ByName_RespectsDirection()
    {
        var col = await NewCollection("C");
        await Insert(File(col, "b.txt"), File(col, "a.txt"), File(col, "c.txt"));

        var asc = await _search.SearchWithSortAsync("", SortColumn.Name, SortDirection.Asc, collectionIds: new[] { col });
        var desc = await _search.SearchWithSortAsync("", SortColumn.Name, SortDirection.Desc, collectionIds: new[] { col });

        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, asc.Files.Select(f => f.Name));
        Assert.Equal(new[] { "c.txt", "b.txt", "a.txt" }, desc.Files.Select(f => f.Name));
    }

    [Fact]
    public async Task SearchWithSort_BySize_OrdersAscending()
    {
        var col = await NewCollection("C");
        await Insert(
            File(col, "big.txt", size: 30),
            File(col, "small.txt", size: 10),
            File(col, "mid.txt", size: 20));

        var result = await _search.SearchWithSortAsync("", SortColumn.Size, SortDirection.Asc, collectionIds: new[] { col });

        Assert.Equal(new long[] { 10, 20, 30 }, result.Files.Select(f => f.SizeBytes));
    }

    // --- Filters -----------------------------------------------------------------------------

    [Fact]
    public async Task SearchWithSort_ExtensionFilter_KeepsOnlyMatchingExtension()
    {
        var col = await NewCollection("C");
        await Insert(File(col, "a.txt"), File(col, "b.log"), File(col, "c.txt"));

        var result = await _search.SearchWithSortAsync(
            "", SortColumn.Name, SortDirection.Asc, collectionIds: new[] { col },
            extensionFilter: new[] { ".txt" });

        Assert.Equal(new[] { "a.txt", "c.txt" }, result.Files.Select(f => f.Name));
    }

    [Fact]
    public async Task SearchWithSort_DirectoryFilter_KeepsOnlyMatchingDirectory()
    {
        var col = await NewCollection("C");
        await Insert(
            File(col, "x1.txt", dir: @"C:\x"),
            File(col, "y1.txt", dir: @"C:\y"));

        var result = await _search.SearchWithSortAsync(
            "", SortColumn.Name, SortDirection.Asc, collectionIds: new[] { col },
            directoryFilter: @"C:\x");

        Assert.Equal(@"C:\x", Assert.Single(result.Files).Directory);
    }

    [Fact]
    public async Task SearchWithSort_ShowDirectoriesToggle_SelectsFilesOrDirectories()
    {
        var col = await NewCollection("C");
        await Insert(
            File(col, "file.txt", isDirectory: false),
            File(col, "folder", isDirectory: true));

        var filesOnly = await _search.SearchWithSortAsync("", SortColumn.Name, SortDirection.Asc, collectionIds: new[] { col }, showDirectories: false);
        var dirsOnly = await _search.SearchWithSortAsync("", SortColumn.Name, SortDirection.Asc, collectionIds: new[] { col }, showDirectories: true);
        var both = await _search.SearchWithSortAsync("", SortColumn.Name, SortDirection.Asc, collectionIds: new[] { col }, showDirectories: null);

        Assert.False(Assert.Single(filesOnly.Files).IsDirectory);
        Assert.True(Assert.Single(dirsOnly.Files).IsDirectory);
        Assert.Equal(2, both.Files.Count);
    }

    // --- Extension search --------------------------------------------------------------------

    [Theory]
    [InlineData("pdf")]   // no leading dot
    [InlineData(".pdf")]  // with dot
    [InlineData("PDF")]   // upper-case
    public async Task SearchByExtension_NormalizesInput(string ext)
    {
        var col = await NewCollection("C");
        await Insert(File(col, "doc.pdf"), File(col, "note.txt"));

        var result = await _search.SearchByExtensionAsync(ext);

        Assert.Equal("doc.pdf", Assert.Single(result.Files).Name);
    }

    // --- Stats -------------------------------------------------------------------------------

    [Fact]
    public async Task GetStats_ReturnsCountsSizesAndExtensionBreakdown()
    {
        var col = await NewCollection("C");
        await Insert(
            File(col, "a.txt", size: 10),
            File(col, "b.txt", size: 20),
            File(col, "c.log", size: 30));

        var stats = await _search.GetStatsAsync();

        Assert.Equal(3, stats.TotalFiles);
        Assert.Equal(60, stats.TotalSizeBytes);
        Assert.Equal(2, stats.FilesByExtension[".txt"]);
        Assert.Equal(1, stats.FilesByExtension[".log"]);
    }

    // --- Pagination --------------------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_Pagination_LimitsPageButReportsFullTotal()
    {
        var col = await NewCollection("C");
        for (var i = 0; i < 5; i++)
            await Insert(File(col, $"f{i}.txt", modified: DateTime.UtcNow.AddSeconds(i)));

        var page1 = await _search.SearchAsync("", limit: 2, offset: 0, collectionIds: new[] { col });
        var page3 = await _search.SearchAsync("", limit: 2, offset: 4, collectionIds: new[] { col });

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Files.Count);
        Assert.Single(page3.Files); // last leftover row
    }
}
