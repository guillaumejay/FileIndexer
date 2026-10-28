using FileIndexer.Data;
using FileIndexer.Services;

namespace FileIndexer.Tests;

public class ConfigExportServiceTests
{
    [Fact]
    public async Task Serialize_KeepsAccentsReadable()
    {
        using var db = new IndexDbContext(":memory:");
        await db.CreateCollectionAsync("Déjà trié", null, paths: [@"C:\jdr"]);
        var service = new ConfigExportService(db);

        var json = service.Serialize(await service.BuildExportAsync());

        Assert.Contains("Déjà trié", json); // not "D\u00E9j\u00E0": the file is diffed in git
        Assert.Equal("Déjà trié", Assert.Single(service.Deserialize(json)!.Collections).Name);
    }
}
