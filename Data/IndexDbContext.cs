using Microsoft.Data.Sqlite;
using Dapper;
using FileIndexer.Models;

namespace FileIndexer.Data;

public class IndexDbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public IndexDbContext(string dbPath = "fileindex.db")
    {
        _dbPath = dbPath;
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        // Table principale
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                path TEXT NOT NULL UNIQUE,
                directory TEXT NOT NULL,
                extension TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                modified_at_utc TEXT NOT NULL,
                indexed_at_utc TEXT NOT NULL
            )
        """);

        // Index pour les recherches courantes
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_extension ON files(extension)");
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_directory ON files(directory)");
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_modified ON files(modified_at_utc)");

        // Index pour le tri par colonnes
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_name ON files(name)");
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_size ON files(size_bytes)");

        // Table FTS5 pour recherche full-text ultra-rapide
        _connection.Execute("""
            CREATE VIRTUAL TABLE IF NOT EXISTS files_fts USING fts5(
                name, 
                path, 
                directory,
                content='files',
                content_rowid='id',
                tokenize='unicode61 remove_diacritics 2'
            )
        """);

        // Triggers pour garder FTS synchronisé
        _connection.Execute("""
            CREATE TRIGGER IF NOT EXISTS files_ai AFTER INSERT ON files BEGIN
                INSERT INTO files_fts(rowid, name, path, directory) 
                VALUES (new.id, new.name, new.path, new.directory);
            END
        """);

        _connection.Execute("""
            CREATE TRIGGER IF NOT EXISTS files_ad AFTER DELETE ON files BEGIN
                INSERT INTO files_fts(files_fts, rowid, name, path, directory) 
                VALUES ('delete', old.id, old.name, old.path, old.directory);
            END
        """);

        _connection.Execute("""
            CREATE TRIGGER IF NOT EXISTS files_au AFTER UPDATE ON files BEGIN
                INSERT INTO files_fts(files_fts, rowid, name, path, directory) 
                VALUES ('delete', old.id, old.name, old.path, old.directory);
                INSERT INTO files_fts(rowid, name, path, directory) 
                VALUES (new.id, new.name, new.path, new.directory);
            END
        """);
    }

    public async Task<int> InsertFilesAsync(IEnumerable<IndexedFile> files)
    {
        const string sql = """
            INSERT OR REPLACE INTO files 
            (name, path, directory, extension, size_bytes, created_at_utc, modified_at_utc, indexed_at_utc)
            VALUES 
            (@Name, @Path, @Directory, @Extension, @SizeBytes, @CreatedAtUtc, @ModifiedAtUtc, @IndexedAtUtc)
        """;

        var count = 0;
        using var transaction = _connection.BeginTransaction();
        
        foreach (var file in files)
        {
            await _connection.ExecuteAsync(sql, new
            {
                file.Name,
                file.Path,
                file.Directory,
                file.Extension,
                file.SizeBytes,
                CreatedAtUtc = file.CreatedAtUtc.ToString("O"),
                ModifiedAtUtc = file.ModifiedAtUtc.ToString("O"),
                IndexedAtUtc = file.IndexedAtUtc.ToString("O")
            }, transaction);
            count++;
        }
        
        transaction.Commit();
        return count;
    }

    public async Task<int> BulkInsertAsync(IEnumerable<IndexedFile> files, int batchSize = 1000)
    {
        var totalInserted = 0;
        var batch = new List<IndexedFile>(batchSize);

        foreach (var file in files)
        {
            batch.Add(file);
            if (batch.Count >= batchSize)
            {
                totalInserted += await InsertFilesAsync(batch);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            totalInserted += await InsertFilesAsync(batch);
        }

        return totalInserted;
    }

    public async Task<SearchResult> SearchWithSortAsync(
        string query,
        SortColumn sortColumn,
        SortDirection sortDirection,
        int limit = 100,
        int offset = 0)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var isSearch = !string.IsNullOrWhiteSpace(query);

        // Construire ORDER BY
        var orderByColumn = sortColumn switch
        {
            SortColumn.Name => "name",
            SortColumn.Directory => "directory",
            SortColumn.Extension => "extension",
            SortColumn.Size => "size_bytes",
            SortColumn.ModifiedAt => "modified_at_utc",
            SortColumn.Rank when isSearch => "rank",
            _ => "name"
        };
        var orderByDir = sortDirection == SortDirection.Desc ? "DESC" : "ASC";

        if (isSearch)
        {
            var ftsQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(term => $"{term}*"));

            var sql = $"""
                SELECT f.* FROM files f
                INNER JOIN files_fts fts ON f.id = fts.rowid
                WHERE files_fts MATCH @Query
                ORDER BY {orderByColumn} {orderByDir}
                LIMIT @Limit OFFSET @Offset
                """;

            var results = await _connection.QueryAsync<IndexedFileDto>(sql,
                new { Query = ftsQuery, Limit = limit, Offset = offset });

            var countSql = """
                SELECT COUNT(*) FROM files f
                INNER JOIN files_fts fts ON f.id = fts.rowid
                WHERE files_fts MATCH @Query
                """;
            var total = await _connection.ExecuteScalarAsync<int>(countSql, new { Query = ftsQuery });

            sw.Stop();
            return new SearchResult
            {
                Files = results.Select(MapToIndexedFile).ToList(),
                TotalCount = total,
                SearchDuration = sw.Elapsed
            };
        }
        else
        {
            var sql = $"SELECT * FROM files ORDER BY {orderByColumn} {orderByDir} LIMIT @Limit OFFSET @Offset";
            var allFiles = await _connection.QueryAsync<IndexedFileDto>(sql,
                new { Limit = limit, Offset = offset });

            var totalCount = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM files");

            sw.Stop();
            return new SearchResult
            {
                Files = allFiles.Select(MapToIndexedFile).ToList(),
                TotalCount = totalCount,
                SearchDuration = sw.Elapsed
            };
        }
    }

    public async Task<SearchResult> SearchAsync(string query, int limit = 100, int offset = 0)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        if (string.IsNullOrWhiteSpace(query))
        {
            var allFiles = await _connection.QueryAsync<IndexedFileDto>(
                "SELECT * FROM files ORDER BY modified_at_utc DESC LIMIT @Limit OFFSET @Offset",
                new { Limit = limit, Offset = offset });
            
            var totalCount = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM files");
            
            sw.Stop();
            return new SearchResult
            {
                Files = allFiles.Select(MapToIndexedFile).ToList(),
                TotalCount = totalCount,
                SearchDuration = sw.Elapsed
            };
        }

        // Préparer la requête FTS5 (ajouter * pour préfixe matching)
        var ftsQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(term => $"{term}*"));

        var sql = """
            SELECT f.* FROM files f
            INNER JOIN files_fts fts ON f.id = fts.rowid
            WHERE files_fts MATCH @Query
            ORDER BY rank
            LIMIT @Limit OFFSET @Offset
        """;

        var results = await _connection.QueryAsync<IndexedFileDto>(sql, 
            new { Query = ftsQuery, Limit = limit, Offset = offset });

        var countSql = """
            SELECT COUNT(*) FROM files f
            INNER JOIN files_fts fts ON f.id = fts.rowid
            WHERE files_fts MATCH @Query
        """;
        
        var total = await _connection.ExecuteScalarAsync<int>(countSql, new { Query = ftsQuery });

        sw.Stop();
        return new SearchResult
        {
            Files = results.Select(MapToIndexedFile).ToList(),
            TotalCount = total,
            SearchDuration = sw.Elapsed
        };
    }

    public async Task<SearchResult> SearchByExtensionAsync(string extension, int limit = 100, int offset = 0)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var normalizedExt = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        
        var files = await _connection.QueryAsync<IndexedFileDto>(
            "SELECT * FROM files WHERE extension = @Extension ORDER BY modified_at_utc DESC LIMIT @Limit OFFSET @Offset",
            new { Extension = normalizedExt, Limit = limit, Offset = offset });
        
        var total = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE extension = @Extension",
            new { Extension = normalizedExt });

        sw.Stop();
        return new SearchResult
        {
            Files = files.Select(MapToIndexedFile).ToList(),
            TotalCount = total,
            SearchDuration = sw.Elapsed
        };
    }

    public async Task<IndexStats> GetStatsAsync()
    {
        var stats = new IndexStats();

        stats.TotalFiles = await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM files");
        stats.TotalSizeBytes = await _connection.ExecuteScalarAsync<long>("SELECT COALESCE(SUM(size_bytes), 0) FROM files");
        
        var lastIndexed = await _connection.ExecuteScalarAsync<string?>(
            "SELECT MAX(indexed_at_utc) FROM files");
        if (!string.IsNullOrEmpty(lastIndexed))
        {
            stats.LastIndexedAtUtc = DateTime.Parse(lastIndexed);
        }

        var extensions = await _connection.QueryAsync<(string Extension, int Count)>(
            "SELECT extension, COUNT(*) as Count FROM files GROUP BY extension ORDER BY Count DESC LIMIT 20");
        
        stats.FilesByExtension = extensions.ToDictionary(e => e.Extension, e => e.Count);

        return stats;
    }

    public async Task ClearAsync()
    {
        await _connection.ExecuteAsync("DELETE FROM files");
        await _connection.ExecuteAsync("DELETE FROM files_fts");
    }

    public async Task<bool> FileExistsAsync(string path, DateTime modifiedAtUtc)
    {
        var result = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE path = @Path AND modified_at_utc = @ModifiedAtUtc",
            new { Path = path, ModifiedAtUtc = modifiedAtUtc.ToString("O") });
        return result > 0;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    // DTO pour le mapping Dapper
    private class IndexedFileDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Directory { get; set; } = "";
        public string Extension { get; set; } = "";
        public long Size_Bytes { get; set; }
        public string Created_At_Utc { get; set; } = "";
        public string Modified_At_Utc { get; set; } = "";
        public string Indexed_At_Utc { get; set; } = "";
    }

    private static IndexedFile MapToIndexedFile(IndexedFileDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        Path = dto.Path,
        Directory = dto.Directory,
        Extension = dto.Extension,
        SizeBytes = dto.Size_Bytes,
        CreatedAtUtc = DateTime.Parse(dto.Created_At_Utc),
        ModifiedAtUtc = DateTime.Parse(dto.Modified_At_Utc),
        IndexedAtUtc = DateTime.Parse(dto.Indexed_At_Utc)
    };
}
