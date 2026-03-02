using FileIndexer.Data;
using FileIndexer.Models;
using Microsoft.Extensions.Logging;

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

    public async Task<SearchResult> SearchAsync(string query, int limit = 100, int offset = 0, IEnumerable<int>? collectionIds = null)
    {
        _logger.LogDebug("Search: {Query}, Collections: {Collections}", query, collectionIds != null ? string.Join(",", collectionIds) : "all");
        return await _db.SearchAsync(query, limit, offset, collectionIds);
    }

    public async Task<SearchResult> SearchWithSortAsync(
        string query,
        SortColumn sortColumn,
        SortDirection sortDirection,
        int limit = 100,
        int offset = 0,
        IEnumerable<int>? collectionIds = null,
        IEnumerable<string>? extensionFilter = null,
        string? directoryFilter = null)
    {
        _logger.LogDebug("Search with sort: {Query}, {Column} {Direction}, Collections: {Collections}, Directory: {Directory}",
            query, sortColumn, sortDirection, collectionIds != null ? string.Join(",", collectionIds) : "all", directoryFilter ?? "all");
        return await _db.SearchWithSortAsync(query, sortColumn, sortDirection, limit, offset, collectionIds, extensionFilter, directoryFilter);
    }

    public async Task<SearchResult> SearchByExtensionAsync(string extension, int limit = 100, int offset = 0, IEnumerable<int>? collectionIds = null)
    {
        _logger.LogDebug("Search by extension: {Extension}, Collections: {Collections}",
            extension, collectionIds != null ? string.Join(",", collectionIds) : "all");
        return await _db.SearchByExtensionAsync(extension, limit, offset, collectionIds);
    }

    public async Task<IndexStats> GetStatsAsync(IEnumerable<int>? collectionIds = null)
    {
        return await _db.GetStatsAsync(collectionIds);
    }
}
