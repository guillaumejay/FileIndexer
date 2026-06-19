using Microsoft.Data.Sqlite;
using Dapper;
using FileIndexer.Models;

namespace FileIndexer.Data;

public class IndexDbContext : IDisposable
{
    private readonly string _connectionString;

    // For in-memory databases, a single keep-alive connection must stay open for the
    // lifetime of the context, otherwise the shared-cache in-memory DB is destroyed as
    // soon as the last connection closes. Null for file-based databases.
    private readonly SqliteConnection? _keepAlive;

    public IndexDbContext(string dbPath = "fileindex.db")
    {
        var inMemory = string.IsNullOrWhiteSpace(dbPath) || dbPath == ":memory:";
        if (inMemory)
        {
            // Shared-cache in-memory DB so every pooled connection sees the same data.
            // A unique name keeps independent contexts (e.g. tests) isolated from each other.
            var name = "fileindexer_" + Guid.NewGuid().ToString("N");
            _connectionString = $"Data Source={name};Mode=Memory;Cache=Shared";
            _keepAlive = CreateConnection();
        }
        else
        {
            _connectionString = $"Data Source={dbPath}";
        }

        InitializeDatabase();
    }

    // Opens a fresh pooled connection. Each database operation uses its own connection so
    // the context is safe to use concurrently (e.g. parallel scanner reads + writer inserts).
    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // Wait instead of failing immediately when the DB is briefly locked by another connection.
        connection.Execute("PRAGMA busy_timeout=5000;");
        return connection;
    }

    private void InitializeDatabase()
    {
        using var connection = CreateConnection();

        // WAL allows concurrent readers alongside a single writer (no-op for in-memory DBs).
        if (_keepAlive == null)
        {
            connection.Execute("PRAGMA journal_mode=WAL;");
        }

        // Collections table
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS collections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                description TEXT,
                created_at_utc TEXT NOT NULL
            )
        """);

        // Collection paths table
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS collection_paths (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                path TEXT NOT NULL,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
            )
        """);
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_collection_paths_collection ON collection_paths(collection_id)");

        // Files table (path NOT unique - files can exist in multiple collections)
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                path TEXT NOT NULL,
                directory TEXT NOT NULL,
                extension TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                is_directory INTEGER NOT NULL DEFAULT 0,
                created_at_utc TEXT NOT NULL,
                modified_at_utc TEXT NOT NULL,
                indexed_at_utc TEXT NOT NULL,
                FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
            )
        """);

        // Migrations for existing databases. Check first so no exception is thrown on an
        // already-upgraded schema (a caught exception still trips the debugger every startup).
        if (!ColumnExists(connection, "files", "is_directory"))
            connection.Execute("ALTER TABLE files ADD COLUMN is_directory INTEGER NOT NULL DEFAULT 0");
        if (!ColumnExists(connection, "collections", "excluded_directories"))
            connection.Execute("ALTER TABLE collections ADD COLUMN excluded_directories TEXT NOT NULL DEFAULT '__MACOSX'");

        // Indexes for common searches
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_extension ON files(extension)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_directory ON files(directory)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_modified ON files(modified_at_utc)");

        // Indexes for column sorting
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_name ON files(name)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_size ON files(size_bytes)");

        // FTS5 virtual table for ultra-fast full-text search
        connection.Execute("""
            CREATE VIRTUAL TABLE IF NOT EXISTS files_fts USING fts5(
                name,
                path,
                directory,
                content='files',
                content_rowid='id',
                tokenize='unicode61 remove_diacritics 2'
            )
        """);

        // Triggers to keep FTS in sync with the files table
        connection.Execute("""
            CREATE TRIGGER IF NOT EXISTS files_ai AFTER INSERT ON files BEGIN
                INSERT INTO files_fts(rowid, name, path, directory)
                VALUES (new.id, new.name, new.path, new.directory);
            END
        """);

        connection.Execute("""
            CREATE TRIGGER IF NOT EXISTS files_ad AFTER DELETE ON files BEGIN
                INSERT INTO files_fts(files_fts, rowid, name, path, directory)
                VALUES ('delete', old.id, old.name, old.path, old.directory);
            END
        """);

        connection.Execute("""
            CREATE TRIGGER IF NOT EXISTS files_au AFTER UPDATE ON files BEGIN
                INSERT INTO files_fts(files_fts, rowid, name, path, directory)
                VALUES ('delete', old.id, old.name, old.path, old.directory);
                INSERT INTO files_fts(rowid, name, path, directory)
                VALUES (new.id, new.name, new.path, new.directory);
            END
        """);
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        var columns = connection.Query<string>(
            "SELECT name FROM pragma_table_info(@Table)", new { Table = table });
        return columns.Any(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<int> InsertFilesAsync(IEnumerable<IndexedFile> files)
    {
        const string sql = """
            INSERT INTO files
            (collection_id, name, path, directory, extension, size_bytes, is_directory, created_at_utc, modified_at_utc, indexed_at_utc)
            VALUES
            (@CollectionId, @Name, @Path, @Directory, @Extension, @SizeBytes, @IsDirectory, @CreatedAtUtc, @ModifiedAtUtc, @IndexedAtUtc)
        """;

        var count = 0;
        using var connection = CreateConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var file in files)
        {
            await connection.ExecuteAsync(sql, new
            {
                file.CollectionId,
                file.Name,
                file.Path,
                file.Directory,
                file.Extension,
                file.SizeBytes,
                IsDirectory = file.IsDirectory ? 1 : 0,
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
        IEnumerable<int>? collectionIds = null,
        IEnumerable<string>? extensionFilter = null,
        string? directoryFilter = null,
        bool? showDirectories = null)
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

        // Build extension filter clause
        var extensionList = extensionFilter?.ToList();
        var hasExtensionFilter = extensionList != null && extensionList.Count > 0;
        var extensionClause = hasExtensionFilter
            ? $"extension IN ({string.Join(",", extensionList!.Select((_, i) => $"@Ext{i}"))})"
            : "1=1";

        // Build directory filter clause
        var directoryClause = directoryFilter != null ? "f.directory = @DirectoryPath" : "1=1";

        // Build is_directory filter clause
        var isDirClause = showDirectories switch
        {
            true => "f.is_directory = 1",
            false => "f.is_directory = 0",
            null => "1=1"
        };

        var parameters = new DynamicParameters();
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);
        if (hasExtensionFilter)
        {
            for (int i = 0; i < extensionList!.Count; i++)
                parameters.Add($"Ext{i}", extensionList[i]);
        }
        if (directoryFilter != null)
        {
            parameters.Add("DirectoryPath", directoryFilter);
        }

        using var connection = CreateConnection();

        if (isSearch)
        {
            var ftsQuery = BuildFtsQuery(query);

            // A query made only of punctuation (e.g. "+++") produces no tokens; an empty FTS5
            // MATCH expression throws a syntax error, so short-circuit to an empty result.
            if (string.IsNullOrWhiteSpace(ftsQuery))
            {
                sw.Stop();
                return new SearchResult
                {
                    Files = new List<IndexedFile>(),
                    TotalCount = 0,
                    SearchDuration = sw.Elapsed
                };
            }

            parameters.Add("Query", ftsQuery);

            string sql;
            string countSql;

            if (needsDedup)
            {
                // Deduplicate by path when showing all or multiple collections
                sql = $"""
                    SELECT f.* FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause}
                    GROUP BY f.path
                    ORDER BY {orderByColumn} {orderByDir}
                    LIMIT @Limit OFFSET @Offset
                    """;
                countSql = $"""
                    SELECT COUNT(DISTINCT f.path) FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause}
                    """;
            }
            else
            {
                sql = $"""
                    SELECT f.* FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause}
                    ORDER BY {orderByColumn} {orderByDir}
                    LIMIT @Limit OFFSET @Offset
                    """;
                countSql = $"""
                    SELECT COUNT(*) FROM files f
                    INNER JOIN files_fts fts ON f.id = fts.rowid
                    WHERE files_fts MATCH @Query AND {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause}
                    """;
            }

            var results = await connection.QueryAsync<IndexedFileDto>(sql, parameters);
            var total = await connection.ExecuteScalarAsync<int>(countSql, parameters);

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
                sql = $"SELECT * FROM files f WHERE {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause} GROUP BY f.path ORDER BY {orderByColumn} {orderByDir} LIMIT @Limit OFFSET @Offset";
                countSql = $"SELECT COUNT(DISTINCT f.path) FROM files f WHERE {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause}";
            }
            else
            {
                sql = $"SELECT * FROM files f WHERE {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause} ORDER BY {orderByColumn} {orderByDir} LIMIT @Limit OFFSET @Offset";
                countSql = $"SELECT COUNT(*) FROM files f WHERE {collectionClause} AND {extensionClause} AND {directoryClause} AND {isDirClause}";
            }

            var allFiles = await connection.QueryAsync<IndexedFileDto>(sql, parameters);
            var totalCount = await connection.ExecuteScalarAsync<int>(countSql, parameters);

            sw.Stop();
            return new SearchResult
            {
                Files = allFiles.Select(MapToIndexedFile).ToList(),
                TotalCount = totalCount,
                SearchDuration = sw.Elapsed
            };
        }
    }

    // Builds an FTS5 MATCH expression from raw user input.
    // The unicode61 tokenizer strips punctuation and splits on non-alphanumeric chars,
    // so "D&D" is indexed as two adjacent tokens "d" and "d". We replicate this splitting:
    // - Simple words like "animist" become prefix searches: animist*
    // - Words with punctuation like "d&d" are split into sub-tokens ("d","d")
    //   and combined with NEAR(..., 0) to require them adjacent, matching the original text.
    // Returns an empty string when the input yields no usable tokens (e.g. only punctuation),
    // so callers can avoid issuing an invalid empty MATCH.
    internal static string BuildFtsQuery(string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ftsTerms = new List<string>();
        foreach (var term in terms)
        {
            var tokens = System.Text.RegularExpressions.Regex.Split(term, @"[^\p{L}\p{N}]+")
                .Where(t => t.Length > 0)
                .ToList();

            if (tokens.Count > 1)
            {
                // Punctuation produced multiple sub-tokens: use NEAR to require adjacency
                // e.g. "d&d" -> NEAR("d" "d", 0)
                ftsTerms.Add($"NEAR({string.Join(" ", tokens.Select(t => $"\"{t}\""))}, 0)");
            }
            else if (tokens.Count == 1)
            {
                // Single token: prefix search to match partial words
                // e.g. "anim" -> anim*  (matches "animist", "animation", etc.)
                ftsTerms.Add($"{tokens[0]}*");
            }
        }
        return string.Join(" ", ftsTerms);
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

        using var connection = CreateConnection();
        var files = await connection.QueryAsync<IndexedFileDto>(sql,
            new { Extension = normalizedExt, Limit = limit, Offset = offset });
        var total = await connection.ExecuteScalarAsync<int>(countSql, new { Extension = normalizedExt });

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

        using var connection = CreateConnection();

        if (needsDedup)
        {
            // Count unique paths when showing all or multiple collections
            stats.TotalFiles = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(DISTINCT path) FROM files WHERE {collectionClause}");
            // For size, we need to avoid double-counting - use subquery to get distinct paths first
            stats.TotalSizeBytes = await connection.ExecuteScalarAsync<long>(
                $"SELECT COALESCE(SUM(size_bytes), 0) FROM (SELECT path, size_bytes FROM files WHERE {collectionClause} GROUP BY path)");
        }
        else
        {
            stats.TotalFiles = await connection.ExecuteScalarAsync<int>(
                $"SELECT COUNT(*) FROM files WHERE {collectionClause}");
            stats.TotalSizeBytes = await connection.ExecuteScalarAsync<long>(
                $"SELECT COALESCE(SUM(size_bytes), 0) FROM files WHERE {collectionClause}");
        }

        var lastIndexed = await connection.ExecuteScalarAsync<string?>(
            $"SELECT MAX(indexed_at_utc) FROM files WHERE {collectionClause}");
        if (!string.IsNullOrEmpty(lastIndexed))
        {
            stats.LastIndexedAtUtc = DateTime.Parse(lastIndexed);
        }

        string extensionSql = needsDedup
            ? $"SELECT extension, COUNT(DISTINCT path) as Count FROM files WHERE {collectionClause} GROUP BY extension ORDER BY Count DESC LIMIT 20"
            : $"SELECT extension, COUNT(*) as Count FROM files WHERE {collectionClause} GROUP BY extension ORDER BY Count DESC LIMIT 20";

        var extensions = await connection.QueryAsync<(string Extension, int Count)>(extensionSql);
        stats.FilesByExtension = extensions.ToDictionary(e => e.Extension, e => e.Count);

        return stats;
    }

    public async Task ClearAsync()
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync("DELETE FROM files");
        await connection.ExecuteAsync("DELETE FROM files_fts");
    }

    public async Task ClearCollectionAsync(int collectionId)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });
    }

    public async Task<bool> FileExistsAsync(string path, DateTime modifiedAtUtc)
    {
        using var connection = CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE path = @Path AND modified_at_utc = @ModifiedAtUtc",
            new { Path = path, ModifiedAtUtc = modifiedAtUtc.ToString("O") });
        return result > 0;
    }

    public async Task<bool> FileExistsInCollectionAsync(string path, int collectionId, DateTime modifiedAtUtc)
    {
        using var connection = CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE path = @Path AND collection_id = @CollectionId AND modified_at_utc = @ModifiedAtUtc",
            new { Path = path, CollectionId = collectionId, ModifiedAtUtc = modifiedAtUtc.ToString("O") });
        return result > 0;
    }

    public void Dispose()
    {
        _keepAlive?.Dispose();
    }

    // File operations methods
    public async Task<IndexedFile?> GetFileByIdAsync(long id)
    {
        using var connection = CreateConnection();
        var dto = await connection.QuerySingleOrDefaultAsync<IndexedFileDto>(
            "SELECT * FROM files WHERE id = @Id", new { Id = id });
        return dto == null ? null : MapToIndexedFile(dto);
    }

    public async Task<List<IndexedFile>> GetFilesByIdsAsync(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return new List<IndexedFile>();

        using var connection = CreateConnection();
        var dtos = await connection.QueryAsync<IndexedFileDto>(
            "SELECT * FROM files WHERE id IN @Ids", new { Ids = idList });
        return dtos.Select(MapToIndexedFile).ToList();
    }

    public async Task UpdateFilePathAsync(long id, string newPath, string newDirectory, string newName, string newExtension)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync("""
            UPDATE files
            SET path = @Path, directory = @Directory, name = @Name, extension = @Extension
            WHERE id = @Id
            """, new { Id = id, Path = newPath, Directory = newDirectory, Name = newName, Extension = newExtension });
    }

    public async Task DeleteFilesByIdsAsync(IEnumerable<long> ids)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return;

        using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM files WHERE id IN @Ids", new { Ids = idList });
    }

    // Collection methods
    public async Task<List<Collection>> GetCollectionsAsync()
    {
        List<CollectionDto> collections;
        using (var connection = CreateConnection())
        {
            collections = (await connection.QueryAsync<CollectionDto>(
                "SELECT * FROM collections ORDER BY name")).ToList();
        }

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
        CollectionDto? dto;
        using (var connection = CreateConnection())
        {
            dto = await connection.QuerySingleOrDefaultAsync<CollectionDto>(
                "SELECT * FROM collections WHERE id = @Id", new { Id = id });
        }
        if (dto == null) return null;

        var collection = MapToCollection(dto);
        collection.Paths = await GetCollectionPathsAsync(id);
        var stats = await GetCollectionStatsAsync(id);
        collection.FileCount = stats.FileCount;
        collection.LastIndexedAtUtc = stats.LastIndexedAtUtc;
        return collection;
    }

    public async Task<Collection> CreateCollectionAsync(string name, string? description, string excludedDirectories = "__MACOSX")
    {
        var now = DateTime.UtcNow.ToString("O");
        using var connection = CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>("""
            INSERT INTO collections (name, description, created_at_utc, excluded_directories)
            VALUES (@Name, @Description, @CreatedAtUtc, @ExcludedDirectories);
            SELECT last_insert_rowid();
            """, new { Name = name, Description = description, CreatedAtUtc = now, ExcludedDirectories = excludedDirectories });

        return new Collection
        {
            Id = id,
            Name = name,
            Description = description,
            ExcludedDirectories = excludedDirectories,
            CreatedAtUtc = DateTime.Parse(now)
        };
    }

    public async Task UpdateCollectionAsync(int id, string name, string? description, string? excludedDirectories = null)
    {
        using var connection = CreateConnection();
        if (excludedDirectories != null)
        {
            await connection.ExecuteAsync(
                "UPDATE collections SET name = @Name, description = @Description, excluded_directories = @ExcludedDirectories WHERE id = @Id",
                new { Id = id, Name = name, Description = description, ExcludedDirectories = excludedDirectories });
        }
        else
        {
            await connection.ExecuteAsync(
                "UPDATE collections SET name = @Name, description = @Description WHERE id = @Id",
                new { Id = id, Name = name, Description = description });
        }
    }

    public async Task DeleteCollectionAsync(int id)
    {
        using var connection = CreateConnection();
        // CASCADE will delete collection_paths and files
        await connection.ExecuteAsync("DELETE FROM collections WHERE id = @Id", new { Id = id });
    }

    public async Task<List<CollectionPath>> GetCollectionPathsAsync(int collectionId)
    {
        using var connection = CreateConnection();
        var paths = await connection.QueryAsync<CollectionPathDto>(
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
        using var connection = CreateConnection();
        var id = await connection.ExecuteScalarAsync<int>("""
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
        using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM collection_paths WHERE id = @Id", new { Id = pathId });
    }

    public async Task<List<PathOverlap>> CheckPathOverlapsAsync(int excludeCollectionId, string newPath)
    {
        var normalizedNewPath = NormalizePath(newPath);
        IEnumerable<(int Id, int Collection_Id, string Path, string CollectionName)> allPaths;
        using (var connection = CreateConnection())
        {
            allPaths = await connection.QueryAsync<(int Id, int Collection_Id, string Path, string CollectionName)>("""
                SELECT cp.id, cp.collection_id, cp.path, c.name as CollectionName
                FROM collection_paths cp
                JOIN collections c ON c.id = cp.collection_id
                WHERE cp.collection_id != @ExcludeCollectionId
                """, new { ExcludeCollectionId = excludeCollectionId });
        }

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
        using var connection = CreateConnection();
        var fileCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });

        var lastIndexed = await connection.ExecuteScalarAsync<string?>(
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
        ExcludedDirectories = dto.Excluded_Directories ?? "__MACOSX",
        CreatedAtUtc = DateTime.Parse(dto.Created_At_Utc)
    };

    private class CollectionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Created_At_Utc { get; set; } = "";
        public string Excluded_Directories { get; set; } = "__MACOSX";
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
        public int Is_Directory { get; set; }
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
        IsDirectory = dto.Is_Directory != 0,
        CreatedAtUtc = DateTime.Parse(dto.Created_At_Utc),
        ModifiedAtUtc = DateTime.Parse(dto.Modified_At_Utc),
        IndexedAtUtc = DateTime.Parse(dto.Indexed_At_Utc)
    };
}
