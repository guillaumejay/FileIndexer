using FileIndexer.Data;
using FileIndexer.Models;

namespace FileIndexer.Services;

public class SearchService
{
    private readonly IndexDbContext _db;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IndexDbContext db, ILogger<SearchService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SearchResult> SearchAsync(string query, int limit = 100, int offset = 0)
    {
        _logger.LogDebug("Recherche: {Query}", query);
        return await _db.SearchAsync(query, limit, offset);
    }

    public async Task<SearchResult> SearchWithSortAsync(
        string query,
        SortColumn sortColumn,
        SortDirection sortDirection,
        int limit = 100,
        int offset = 0)
    {
        _logger.LogDebug("Recherche avec tri: {Query}, {Column} {Direction}", query, sortColumn, sortDirection);
        return await _db.SearchWithSortAsync(query, sortColumn, sortDirection, limit, offset);
    }

    public async Task<SearchResult> SearchByExtensionAsync(string extension, int limit = 100, int offset = 0)
    {
        _logger.LogDebug("Recherche par extension: {Extension}", extension);
        return await _db.SearchByExtensionAsync(extension, limit, offset);
    }

    public async Task<IndexStats> GetStatsAsync()
    {
        return await _db.GetStatsAsync();
    }
}
