using System.Reflection;
using FileIndexer.Data;
using FileIndexer.Models;
using Microsoft.Extensions.Logging;

namespace FileIndexer.Services;

// Trace of the changes FileOperationsService makes on disk, one entry per item.
public interface IFileOperationJournal
{
    // destination: the new path (move, rename, copy), or null (delete). error: null on success.
    Task RecordAsync(string operation, string path, string? destination, string? error = null);
}

// Stores the journal in the index database (table "operations"), so it can be counted.
// A journal failure is logged, never thrown: it must not turn a done move into an error.
public sealed class IndexOperationJournal(IndexDbContext db, ILogger<IndexOperationJournal> logger) : IFileOperationJournal
{
    private static readonly string Source = Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown";

    public async Task RecordAsync(string operation, string path, string? destination, string? error = null)
    {
        try
        {
            await db.RecordOperationAsync(new FileOperationEntry(DateTime.UtcNow, operation, path, destination, error, Source));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cannot journal {Operation} of {Path}", operation, path);
        }
    }
}
