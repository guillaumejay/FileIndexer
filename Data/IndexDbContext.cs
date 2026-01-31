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
        // Collections table
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS collections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                description TEXT,
                created_at_utc TEXT NOT NULL
            )
        """);

        // Collection paths table
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS collection_paths (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                path TEXT NOT NULL,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
            )
        """);
        _connection.Execute("CREATE INDEX IF NOT EXISTS idx_collection_paths_collection ON collection_paths(collection_id)");

        // Files table (path NOT unique - files can exist in multiple collections)
        _connection.Execute("""
            CREATE TABLE IF NOT EXISTS files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                path TEXT NOT NULL,
                directory TEXT NOT NULL,
                extension TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                created_at_utc TEXT NOT NULL,
                modified_at_utc TEXT NOT NULL,
                indexed_at_utc TEXT NOT NULL,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
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
            INSERT INTO files
            (collection_id, name, path, directory, extension, size_bytes, created_at_utc, modified_at_utc, indexed_at_utc)
            VALUES
            (@CollectionId, @Name, @Path, @Directory, @Extension, @SizeBytes, @CreatedAtUtc, @ModifiedAtUtc, @IndexedAtUtc)
        """;

        var count = 0;
        using var transaction = _connection.BeginTransaction();

        foreach (var file in files)
        {
            await _connection.ExecuteAsync(sql, new
            {
                file.CollectionId,
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
        int offset = 0,
        IEnumerable<int>? collectionIds = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var isSearch = !string.IsNullOrWhiteSpace(query);
        var collectionIdList = collectionIds?.ToList();
        var hasCollectionFilter = collectionIdList != null && collectionIdList.Count > 0;
        var needsDedup = !hasCollectionFilter || collectionIdList!.Count > 1;

        // Build ORDER BY
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

        // Build collection filter clause
        var collectionClause = hasCollectionFilter
            ? $"collection_id IN ({string.Join(",", collectionIdList!)})"
            : "1=1";

        if (isSearch)
        {
            var ftsQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(term => $"{term}*"));

            string sql;
            string countSql;

            if (needsDedup)
            {
                // Deduplicate by path when showing all or multiple collections
                sql = $"""
                    SELECT f.* FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause}
                    GROUP BY f.path
                    ORDER BY {orderByColumn} {orderByDir}
                    LIMIT @Limit OFFSET @Offset
                    """;
                countSql = $"""
                    SELECT COUNT(DISTINCT f.path) FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause}
                    """;
            }
            else
            {
                sql = $"""
                    SELECT f.* FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause}
                    ORDER BY {orderByColumn} {orderByDir}
                    LIMIT @Limit OFFSET @Offset
                    """;
                countSql = $"""
                    SELECT COUNT(*) FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause}
                    """;
            }

            var results = await _connection.QueryAsync<IndexedFileDto>(sql,
                new { Query = ftsQuery, Limit = limit, Offset = offset });
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
            string sql;
            string countSql;

            if (needsDedup)
            {
                sql = $"SELECT * FROM files WHERE {collectionClause} GROUP BY path ORDER BY {orderByColumn} {orderByDir} LIMIT @Limit OFFSET @Offset";
                countSql = $"SELECT COUNT(DISTINCT path) FROM files WHERE {collectionClause}";
            }
            else
            {
                sql = $"SELECT * FROM files WHERE {collectionClause} ORDER BY {orderByColumn} {orderByDir} LIMIT @Limit OFFSET @Offset";
                countSql = $"SELECT COUNT(*) FROM files WHERE {collectionClause}";
            }

            var allFiles = await _connection.QueryAsync<IndexedFileDto>(sql,
                new { Limit = limit, Offset = offset });
            var totalCount = await _connection.ExecuteScalarAsync<int>(countSql);

            sw.Stop();
            return new SearchResult
            {
                Files = allFiles.Select(MapToIndexedFile).ToList(),
                TotalCount = totalCount,
                SearchDuration = sw.Elapsed
            };
        }
    }

    public async Task<SearchResult> SearchAsync(string query, int limit = 100, int offset = 0, IEnumerable<int>? collectionIds = null)
    {
        return await SearchWithSortAsync(query, SortColumn.ModifiedAt, SortDirection.Desc, limit, offset, collectionIds);
    }

    public async Task<SearchResult> SearchByExtensionAsync(string extension, int limit = 100, int offset = 0, IEnumerable<int>? collectionIds = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var normalizedExt = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        var collectionIdList = collectionIds?.ToList();
        var hasCollectionFilter = collectionIdList != null && collectionIdList.Count > 0;
        var needsDedup = !hasCollectionFilter || collectionIdList!.Count > 1;

        var collectionClause = hasCollectionFilter
            ? $"collection_id IN ({string.Join(",", collectionIdList!)})"
            : "1=1";

        string sql;
        string countSql;

        if (needsDedup)
        {
            sql = $"SELECT * FROM files WHERE extension = @Extension AND {collectionClause} GROUP BY path ORDER BY modified_at_utc DESC LIMIT @Limit OFFSET @Offset";
            countSql = $"SELECT COUNT(DISTINCT path) FROM files WHERE extension = @Extension AND {collectionClause}";
        }
        else
        {
            sql = $"SELECT * FROM files WHERE extension = @Extension AND {collectionClause} ORDER BY modified_at_utc DESC LIMIT @Limit OFFSET @Offset";
            countSql = $"SELECT COUNT(*) FROM files WHERE extension = @Extension AND {collectionClause}";
        }

        var files = await _connection.QueryAsync<IndexedFileDto>(sql,
            new { Extension = normalizedExt, Limit = limit, Offset = offset });
        var total = await _connection.ExecuteScalarAsync<int>(countSql, new { Extension = normalizedExt });

        sw.Stop();
        return new SearchResult
        {
            Files = files.Select(MapToIndexedFile).ToList(),
            TotalCount = total,
            SearchDuration = sw.Elapsed
        };
    }

    public async Task<IndexStats> GetStatsAsync(IEnumerable<int>? collectionIds = null)
    {
        var stats = new IndexStats();
        var collectionIdList = collectionIds?.ToList();
        var hasCollectionFilter = collectionIdList != null && collectionIdList.Count > 0;
        var needsDedup = !hasCollectionFilter || collectionIdList!.Count > 1;

        var collectionClause = hasCollectionFilter
            ? $"collection_id IN ({string.Join(",", collectionIdList!)})"
            : "1=1";

        if (needsDedup)
        {
            // Count unique paths when showing all or multiple collections
            stats.TotalFiles = await _connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(DISTINCT path) FROM files WHERE {collectionClause}");
            // For size, we need to avoid double-counting - use subquery to get distinct paths first
            stats.TotalSizeBytes = await _connection.ExecuteScalarAsync<long>(
                $"SELECT COALESCE(SUM(size_bytes), 0) FROM (SELECT path, size_bytes FROM files WHERE {collectionClause} GROUP BY path)");
        }
        else
        {
            stats.TotalFiles = await _connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM files WHERE {collectionClause}");
            stats.TotalSizeBytes = await _connection.ExecuteScalarAsync<long>(
                $"SELECT COALESCE(SUM(size_bytes), 0) FROM files WHERE {collectionClause}");
        }

        var lastIndexed = await _connection.ExecuteScalarAsync<string?>(
            $"SELECT MAX(indexed_at_utc) FROM files WHERE {collectionClause}");
        if (!string.IsNullOrEmpty(lastIndexed))
        {
            stats.LastIndexedAtUtc = DateTime.Parse(lastIndexed);
        }

        string extensionSql = needsDedup
            ? $"SELECT extension, COUNT(DISTINCT path) as Count FROM files WHERE {collectionClause} GROUP BY extension ORDER BY Count DESC LIMIT 20"
            : $"SELECT extension, COUNT(*) as Count FROM files WHERE {collectionClause} GROUP BY extension ORDER BY Count DESC LIMIT 20";

        var extensions = await _connection.QueryAsync<(string Extension, int Count)>(extensionSql);
        stats.FilesByExtension = extensions.ToDictionary(e => e.Extension, e => e.Count);

        return stats;
    }

    public async Task ClearAsync()
    {
        await _connection.ExecuteAsync("DELETE FROM files");
        await _connection.ExecuteAsync("DELETE FROM files_fts");
    }

    public async Task ClearCollectionAsync(int collectionId)
    {
        await _connection.ExecuteAsync(
            "DELETE FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });
    }

    public async Task<bool> FileExistsAsync(string path, DateTime modifiedAtUtc)
    {
        var result = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE path = @Path AND modified_at_utc = @ModifiedAtUtc",
            new { Path = path, ModifiedAtUtc = modifiedAtUtc.ToString("O") });
        return result > 0;
    }

    public async Task<bool> FileExistsInCollectionAsync(string path, int collectionId, DateTime modifiedAtUtc)
    {
        var result = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE path = @Path AND collection_id = @CollectionId AND modified_at_utc = @ModifiedAtUtc",
            new { Path = path, CollectionId = collectionId, ModifiedAtUtc = modifiedAtUtc.ToString("O") });
        return result > 0;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    // Collection methods
    public async Task<List<Collection>> GetCollectionsAsync()
    {
        var collections = (await _connection.QueryAsync<CollectionDto>(
            "SELECT * FROM collections ORDER BY name")).ToList();

        var result = new List<Collection>();
        foreach (var dto in collections)
        {
            var collection = MapToCollection(dto);
            collection.Paths = await GetCollectionPathsAsync(collection.Id);
            var stats = await GetCollectionStatsAsync(collection.Id);
            collection.FileCount = stats.FileCount;
            collection.LastIndexedAtUtc = stats.LastIndexedAtUtc;
            result.Add(collection);
        }
        return result;
    }

    public async Task<Collection?> GetCollectionByIdAsync(int id)
    {
        var dto = await _connection.QuerySingleOrDefaultAsync<CollectionDto>(
            "SELECT * FROM collections WHERE id = @Id", new { Id = id });
        if (dto == null) return null;

        var collection = MapToCollection(dto);
        collection.Paths = await GetCollectionPathsAsync(id);
        var stats = await GetCollectionStatsAsync(id);
        collection.FileCount = stats.FileCount;
        collection.LastIndexedAtUtc = stats.LastIndexedAtUtc;
        return collection;
    }

    public async Task<Collection> CreateCollectionAsync(string name, string? description)
    {
        var now = DateTime.UtcNow.ToString("O");
        var id = await _connection.ExecuteScalarAsync<int>("""
            INSERT INTO collections (name, description, created_at_utc)
            VALUES (@Name, @Description, @CreatedAtUtc);
            SELECT last_insert_rowid();
            """, new { Name = name, Description = description, CreatedAtUtc = now });

        return new Collection
        {
            Id = id,
            Name = name,
            Description = description,
            CreatedAtUtc = DateTime.Parse(now)
        };
    }

    public async Task UpdateCollectionAsync(int id, string name, string? description)
    {
        await _connection.ExecuteAsync(
            "UPDATE collections SET name = @Name, description = @Description WHERE id = @Id",
            new { Id = id, Name = name, Description = description });
    }

    public async Task DeleteCollectionAsync(int id)
    {
        // CASCADE will delete collection_paths and files
        await _connection.ExecuteAsync("DELETE FROM collections WHERE id = @Id", new { Id = id });
    }

    public async Task<List<CollectionPath>> GetCollectionPathsAsync(int collectionId)
    {
        var paths = await _connection.QueryAsync<CollectionPathDto>(
            "SELECT * FROM collection_paths WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });
        return paths.Select(p => new CollectionPath
        {
            Id = (int)p.Id,
            CollectionId = (int)p.Collection_Id,
            Path = p.Path
        }).ToList();
    }

    public async Task<CollectionPath> AddCollectionPathAsync(int collectionId, string path)
    {
        var id = await _connection.ExecuteScalarAsync<int>("""
            INSERT INTO collection_paths (collection_id, path)
            VALUES (@CollectionId, @Path);
            SELECT last_insert_rowid();
            """, new { CollectionId = collectionId, Path = path });

        return new CollectionPath
        {
            Id = id,
            CollectionId = collectionId,
            Path = path
        };
    }

    public async Task RemoveCollectionPathAsync(int pathId)
    {
        await _connection.ExecuteAsync(
            "DELETE FROM collection_paths WHERE id = @Id", new { Id = pathId });
    }

    public async Task<List<PathOverlap>> CheckPathOverlapsAsync(int excludeCollectionId, string newPath)
    {
        var normalizedNewPath = NormalizePath(newPath);
        var allPaths = await _connection.QueryAsync<(int Id, int Collection_Id, string Path, string CollectionName)>("""
            SELECT cp.id, cp.collection_id, cp.path, c.name as CollectionName
            FROM collection_paths cp
            JOIN collections c ON c.id = cp.collection_id
            WHERE cp.collection_id != @ExcludeCollectionId
            """, new { ExcludeCollectionId = excludeCollectionId });

        var overlaps = new List<PathOverlap>();
        foreach (var existing in allPaths)
        {
            var normalizedExisting = NormalizePath(existing.Path);

            // Check if new path is under existing path (existing is parent)
            if (normalizedNewPath.StartsWith(normalizedExisting + Path.DirectorySeparatorChar) ||
                normalizedNewPath == normalizedExisting)
            {
                overlaps.Add(new PathOverlap
                {
                    Path = existing.Path,
                    CollectionName = existing.CollectionName,
                    CollectionId = existing.Collection_Id,
                    IsParent = true
                });
            }
            // Check if existing path is under new path (new is parent)
            else if (normalizedExisting.StartsWith(normalizedNewPath + Path.DirectorySeparatorChar))
            {
                overlaps.Add(new PathOverlap
                {
                    Path = existing.Path,
                    CollectionName = existing.CollectionName,
                    CollectionId = existing.Collection_Id,
                    IsParent = false
                });
            }
        }
        return overlaps;
    }

    public async Task<(int FileCount, DateTime? LastIndexedAtUtc)> GetCollectionStatsAsync(int collectionId)
    {
        var fileCount = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });

        var lastIndexed = await _connection.ExecuteScalarAsync<string?>(
            "SELECT MAX(indexed_at_utc) FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });

        DateTime? lastIndexedAtUtc = null;
        if (!string.IsNullOrEmpty(lastIndexed))
        {
            lastIndexedAtUtc = DateTime.Parse(lastIndexed);
        }

        return (fileCount, lastIndexedAtUtc);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static Collection MapToCollection(CollectionDto dto) => new()
    {
        Id = (int)dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        CreatedAtUtc = DateTime.Parse(dto.Created_At_Utc)
    };

    private class CollectionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Created_At_Utc { get; set; } = "";
    }

    private class CollectionPathDto
    {
        public long Id { get; set; }
        public long Collection_Id { get; set; }
        public string Path { get; set; } = "";
    }

    // DTO pour le mapping Dapper
    private class IndexedFileDto
    {
        public long Id { get; set; }
        public int Collection_Id { get; set; }
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
        CollectionId = dto.Collection_Id,
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
