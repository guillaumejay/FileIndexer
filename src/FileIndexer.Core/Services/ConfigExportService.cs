using System.Text.Json;
using System.Text.Json.Serialization;
using FileIndexer.Data;
using FileIndexer.Models;

namespace FileIndexer.Services;

public class ConfigExportService
{
    private readonly IndexDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConfigExportService(IndexDbContext db)
    {
        _db = db;
    }

    public async Task<ConfigExport> BuildExportAsync(ExportedSettings? settings = null)
    {
        var collections = await _db.GetCollectionsAsync();

        var export = new ConfigExport
        {
            ExportedAtUtc = DateTime.UtcNow,
            Settings = settings,
            Collections = collections.Select(c => new ExportedCollection
            {
                Name = c.Name,
                Description = c.Description,
                Paths = c.Paths.Select(p => p.Path).ToList()
            }).ToList()
        };

        return export;
    }

    public string Serialize(ConfigExport export)
    {
        return JsonSerializer.Serialize(export, JsonOptions);
    }

    public ConfigExport? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ConfigExport>(json, JsonOptions);
    }

    public async Task<ImportResult> ImportCollectionsAsync(ConfigExport config, ImportCollisionStrategy strategy)
    {
        var result = new ImportResult();
        var existingCollections = await _db.GetCollectionsAsync();
        var existingNames = existingCollections.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var exported in config.Collections)
        {
            var name = exported.Name;

            if (existingNames.Contains(name))
            {
                if (strategy == ImportCollisionStrategy.Skip)
                {
                    result.Skipped++;
                    result.Details.Add($"Skipped: \"{name}\" (already exists)");
                    continue;
                }

                // Auto-rename with numeric suffix
                var baseName = name;
                var counter = 2;
                while (existingNames.Contains(name))
                {
                    name = $"{baseName} ({counter})";
                    counter++;
                }
                result.Renamed++;
                result.Details.Add($"Renamed: \"{exported.Name}\" -> \"{name}\"");
            }

            var collection = await _db.CreateCollectionAsync(name, exported.Description);
            existingNames.Add(name);

            foreach (var path in exported.Paths)
            {
                await _db.AddCollectionPathAsync(collection.Id, path);
            }

            result.Imported++;
        }

        return result;
    }
}
