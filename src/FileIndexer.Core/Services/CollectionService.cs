using FileIndexer.Data;
using FileIndexer.Models;

namespace FileIndexer.Services;

public class CollectionService
{
    private readonly IndexDbContext _db;

    public CollectionService(IndexDbContext db)
    {
        _db = db;
    }

    public async Task<List<Collection>> GetAllAsync()
    {
        return await _db.GetCollectionsAsync();
    }

    public async Task<Collection?> GetByIdAsync(int id)
    {
        return await _db.GetCollectionByIdAsync(id);
    }

    public async Task<Collection> CreateAsync(string name, string? description = null, string excludedDirectories = "__MACOSX")
    {
        return await _db.CreateCollectionAsync(name, description, excludedDirectories);
    }

    public async Task UpdateAsync(int id, string name, string? description, string? excludedDirectories = null)
    {
        await _db.UpdateCollectionAsync(id, name, description, excludedDirectories);
    }

    public async Task DeleteAsync(int id)
    {
        await _db.DeleteCollectionAsync(id);
    }

    public async Task<List<CollectionPath>> GetPathsAsync(int collectionId)
    {
        return await _db.GetCollectionPathsAsync(collectionId);
    }

    public async Task<CollectionPath> AddPathAsync(int collectionId, string path)
    {
        return await _db.AddCollectionPathAsync(collectionId, path);
    }

    public async Task RemovePathAsync(int pathId)
    {
        await _db.RemoveCollectionPathAsync(pathId);
    }

    public async Task<List<PathOverlap>> CheckPathOverlapsAsync(int collectionId, string newPath)
    {
        return await _db.CheckPathOverlapsAsync(collectionId, newPath);
    }

    public async Task<(int FileCount, DateTime? LastIndexedAtUtc)> GetStatsAsync(int collectionId)
    {
        return await _db.GetCollectionStatsAsync(collectionId);
    }
}
