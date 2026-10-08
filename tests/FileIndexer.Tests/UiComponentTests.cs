using Bunit;
using FileIndexer.Data;
using FileIndexer.Models;
using FileIndexer.Services;
using FileIndexer.UI.Components;
using FileIndexer.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FileIndexer.Tests;

// Renders the shared Blazor components against an in-memory index (bUnit). Covers the UI
// regressions: keyboard shortcuts leaking from the search box, unconfirmed deletes, and
// editing controls shown on hosts that cannot use them.
public class UiComponentTests : BunitContext
{
    private readonly IndexDbContext _db = new(":memory:");
    private readonly RecordingTrash _trash = new();

    private void Configure(bool fileSystemAccess)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLogging();
        Services.AddFileIndexer(_ => _db, new PlatformCapabilities { HasFileSystemAccess = fileSystemAccess });
        Services.AddSingleton<ITrashService>(_trash);
    }

    private async Task<Collection> SeedAsync()
    {
        var col = await _db.CreateCollectionAsync("docs", null, paths: [@"C:\data"]);
        await _db.UpsertFilesAsync([new IndexedFile
        {
            CollectionId = col.Id,
            Name = "report.txt",
            Path = Path.Combine(Path.GetTempPath(), "report.txt"),
            Directory = Path.GetTempPath(),
            Extension = ".txt",
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow,
            IndexedAtUtc = DateTime.UtcNow
        }]);
        return (await _db.GetCollectionsAsync()).Single();
    }

    [Fact]
    public async Task DeleteKey_InSearchBox_DoesNotTouchSelectedFiles()
    {
        Configure(fileSystemAccess: true);
        var col = await SeedAsync();
        var cut = Render<SearchView>(p => p.Add(v => v.Collections, [col]));
        cut.WaitForElement("tbody tr td.file-name").Click();

        // Neither the search box nor any of its ancestors handles keys: shortcuts live on the
        // file list, which does not contain the search box (previously the page container did,
        // so Delete typed in the box trashed the selection).
        Assert.Throws<MissingEventHandlerException>(() => cut.Find(".search-input").KeyDown("Delete"));
        Assert.Throws<MissingEventHandlerException>(() => cut.Find(".view-container").KeyDown("Delete"));
        Assert.Empty(cut.FindAll(".file-list-container .search-input"));
        Assert.Empty(_trash.Trashed);
    }

    [Fact]
    public async Task DeleteKey_OnList_AsksBeforeTrashing()
    {
        Configure(fileSystemAccess: true);
        var col = await SeedAsync();
        var path = Path.Combine(Path.GetTempPath(), "report.txt");
        File.WriteAllText(path, "x");
        try
        {
            var dialogs = Render<Dialogs>(); // the session's modal host (AppShell renders it)
            var cut = Render<SearchView>(p => p.Add(v => v.Collections, [col]));
            cut.WaitForElement("tbody tr td.file-name").Click();

            cut.Find(".file-list-container").KeyDown("Delete");

            dialogs.WaitForElement(".modal-overlay");
            Assert.Empty(_trash.Trashed); // nothing happens before confirmation
            dialogs.Find(".modal-actions .btn-danger").Click();
            cut.WaitForAssertion(() => Assert.Equal(path, Assert.Single(_trash.Trashed)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CollectionsView_WithoutFileSystemAccess_IsReadOnly()
    {
        Configure(fileSystemAccess: false);
        var col = await SeedAsync();

        var cut = Render<CollectionsView>(p => p.Add(v => v.Collections, [col]));
        cut.WaitForElement(".collection-card");

        Assert.DoesNotContain("New Collection", cut.Markup);
        Assert.DoesNotContain("Reindex all", cut.Markup);
        Assert.Empty(cut.FindAll(".collection-actions"));
    }

    [Fact]
    public async Task CollectionsView_WithFileSystemAccess_OffersEditing()
    {
        Configure(fileSystemAccess: true);
        var col = await SeedAsync();

        var cut = Render<CollectionsView>(p => p.Add(v => v.Collections, [col]));
        cut.WaitForElement(".collection-card");

        Assert.Contains("New Collection", cut.Markup);
        Assert.Single(cut.FindAll(".collection-actions"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _db.Dispose();
    }

    private sealed class RecordingTrash : ITrashService
    {
        public List<string> Trashed { get; } = new();
        public bool IsSupported => true;

        public Task<OperationResult> MoveToTrashAsync(string path)
        {
            lock (Trashed) Trashed.Add(path);
            return Task.FromResult(OperationResult.Success());
        }
    }
}
