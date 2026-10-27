using FileIndexer.Data;
using FileIndexer.Services;

namespace FileIndexer.Tests;

public class ConfigExportServiceTests : IDisposable
{
    private readonly IndexDbContext _db = new(":memory:");
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"fi-export-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        _db.Dispose();
        File.Delete(_file);
    }

    [Fact]
    public async Task WriteExport_RewritesOnlyWhenCollectionsChange()
    {
        var service = new ConfigExportService(_db);
        await _db.CreateCollectionAsync("Déjà trié", null, paths: [@"C:\jdr"]);

        Assert.True(await service.WriteExportAsync(_file));
        var first = await File.ReadAllTextAsync(_file);
        Assert.Contains("Déjà trié", first); // accents kept readable for diffs

        Assert.False(await service.WriteExportAsync(_file)); // same collections: no new date, no diff
        Assert.Equal(first, await File.ReadAllTextAsync(_file));

        await _db.CreateCollectionAsync("Autre", null, paths: [@"C:\autre"]);
        Assert.True(await service.WriteExportAsync(_file));
        Assert.Contains("Autre", await File.ReadAllTextAsync(_file));
    }

    [Fact]
    public async Task WriteExport_OverwritesAFileThatIsNotAnExport()
    {
        await File.WriteAllTextAsync(_file, "not json");

        Assert.True(await new ConfigExportService(_db).WriteExportAsync(_file));
    }
}
