using System.Globalization;
using Microsoft.Data.Sqlite;
using Dapper;
using FileIndexer.Models;

namespace FileIndexer.Data;

public class IndexDbContext : IDisposable
{
    private volatile string _connectionString = "";

    // For in-memory databases, a single keep-alive connection must stay open for the
    // lifetime of the context, otherwise the shared-cache in-memory DB is destroyed as
    // soon as the last connection closes. Null for file-based databases.
    private SqliteConnection? _keepAlive;

    public IndexDbContext(string dbPath = "fileindex.db")
    {
        Open(dbPath);
    }

    public string DatabasePath { get; private set; } = "";

    // Points the context at another database file. Every operation opens its own connection,
    // so callers holding this context switch over on their next call.
    public void SwitchTo(string dbPath)
    {
        var previousKeepAlive = _keepAlive;
        Open(dbPath);
        previousKeepAlive?.Dispose();
        // Release pooled handles on the previous file so it can be replaced or deleted.
        SqliteConnection.ClearAllPools();
    }

    private void Open(string dbPath)
    {
        var inMemory = string.IsNullOrWhiteSpace(dbPath) || dbPath == ":memory:";
        if (inMemory)
        {
            // Shared-cache in-memory DB so every pooled connection sees the same data.
            // A unique name keeps independent contexts (e.g. tests) isolated from each other.
            var name = "fileindexer_" + Guid.NewGuid().ToString("N");
            _connectionString = $"Data Source={name};Mode=Memory;Cache=Shared";
            _keepAlive = CreateConnection();
            DatabasePath = ":memory:";
        }
        else
        {
            _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
            _keepAlive = null;
            DatabasePath = dbPath;
        }

        InitializeDatabase();
    }

    // Opens a fresh pooled connection. Each database operation uses its own connection so
    // the context is safe to use concurrently (e.g. parallel scanner reads + writer inserts).
    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // foreign_keys is per-connection and OFF by default in SQLite: without it the
        // ON DELETE CASCADE clauses are ignored. busy_timeout waits instead of failing
        // immediately when the DB is briefly locked by another connection.
        connection.Execute("PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;");
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

        // Files table: a path is unique within a collection, but may appear in several collections.
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

        // One-time repair before the unique index exists: older versions never enabled foreign
        // keys (orphans survived collection deletion) and re-inserted rows on incremental scans
        // (duplicates). Keep the most recent row per (collection, path).
        if (!IndexExists(connection, "ux_files_collection_path"))
        {
            using var tx = connection.BeginTransaction();
            connection.Execute("DELETE FROM collection_paths WHERE collection_id NOT IN (SELECT id FROM collections)", transaction: tx);
            connection.Execute("DELETE FROM files WHERE collection_id NOT IN (SELECT id FROM collections)", transaction: tx);
            connection.Execute("DELETE FROM files WHERE id NOT IN (SELECT MAX(id) FROM files GROUP BY collection_id, path)", transaction: tx);
            connection.Execute("CREATE UNIQUE INDEX ux_files_collection_path ON files(collection_id, path)", transaction: tx);
            tx.Commit();
        }

        // Indexes for common searches (path: cross-collection dedup and path operations)
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_path ON files(path)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_extension ON files(extension)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_directory ON files(directory)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_modified ON files(modified_at_utc)");

        // Indexes for column sorting
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_name ON files(name)");
        connection.Execute("CREATE INDEX IF NOT EXISTS idx_files_size ON files(size_bytes)");
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        var columns = connection.Query<string>(
            "SELECT name FROM pragma_table_info(@Table)", new { Table = table });
        return columns.Any(c => string.Equals(c, column, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IndexExists(SqliteConnection connection, string index) =>
        connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = @Name", new { Name = index }) > 0;

    // Inserts files, or refreshes the existing row when (collection_id, path) is already indexed.
    // Updating in place keeps the row id stable and the FTS index in sync via the update trigger.
    public async Task<int> UpsertFilesAsync(IEnumerable<IndexedFile> files)
    {
        const string sql = """
            INSERT INTO files
            (collection_id, name, path, directory, extension, size_bytes, is_directory, created_at_utc, modified_at_utc, indexed_at_utc)
            VALUES
            (@CollectionId, @Name, @Path, @Directory, @Extension, @SizeBytes, @IsDirectory, @CreatedAtUtc, @ModifiedAtUtc, @IndexedAtUtc)
            ON CONFLICT(collection_id, path) DO UPDATE SET
                name = excluded.name,
                directory = excluded.directory,
                extension = excluded.extension,
                size_bytes = excluded.size_bytes,
                is_directory = excluded.is_directory,
                created_at_utc = excluded.created_at_utc,
                modified_at_utc = excluded.modified_at_utc,
                indexed_at_utc = excluded.indexed_at_utc
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
                CreatedAtUtc = FormatUtc(file.CreatedAtUtc),
                ModifiedAtUtc = FormatUtc(file.ModifiedAtUtc),
                IndexedAtUtc = FormatUtc(file.IndexedAtUtc)
            }, transaction);
            count++;
        }

        transaction.Commit();
        return count;
    }

    public async Task<int> BulkUpsertAsync(IEnumerable<IndexedFile> files, int batchSize = 1000)
    {
        var total = 0;
        foreach (var batch in files.Chunk(batchSize))
        {
            total += await UpsertFilesAsync(batch);
        }
        return total;
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
        bool? showDirectories = null,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var isSearch = !string.IsNullOrWhiteSpace(query);
        var scope = CollectionScope.From(collectionIds);
        var parameters = new DynamicParameters();
        parameters.Add("Limit", limit);
        parameters.Add("Offset", offset);

        var from = "files f";
        var where = new List<string> { scope.Clause };

        if (isSearch)
        {
            var ftsQuery = BuildFtsQuery(query);

            // A query made only of punctuation (e.g. "+++") produces no tokens; an empty FTS5
            // MATCH expression throws a syntax error, so short-circuit to an empty result.
            if (string.IsNullOrWhiteSpace(ftsQuery))
            {
                return new SearchResult { Files = new List<IndexedFile>(), TotalCount = 0, SearchDuration = sw.Elapsed };
            }

            from = "files f INNER JOIN files_fts fts ON f.id = fts.rowid";
            where.Add("files_fts MATCH @Query");
            parameters.Add("Query", ftsQuery);
        }

        var extensionList = extensionFilter?.ToList();
        if (extensionList is { Count: > 0 })
        {
            where.Add($"f.extension IN ({string.Join(",", extensionList.Select((_, i) => $"@Ext{i}"))})");
            for (var i = 0; i < extensionList.Count; i++)
                parameters.Add($"Ext{i}", extensionList[i]);
        }

        if (directoryFilter != null)
        {
            where.Add("f.directory = @DirectoryPath");
            parameters.Add("DirectoryPath", directoryFilter);
        }

        if (showDirectories.HasValue)
        {
            where.Add(showDirectories.Value ? "f.is_directory = 1" : "f.is_directory = 0");
        }

        var orderByColumn = sortColumn switch
        {
            SortColumn.Name => "f.name",
            SortColumn.Directory => "f.directory",
            SortColumn.Extension => "f.extension",
            SortColumn.Size => "f.size_bytes",
            SortColumn.ModifiedAt => "f.modified_at_utc",
            SortColumn.Rank when isSearch => "rank",
            _ => "f.name"
        };
        var orderByDir = sortDirection == SortDirection.Desc ? "DESC" : "ASC";

        // Deduplicate by path when showing all or multiple collections.
        var whereSql = string.Join(" AND ", where);
        var groupBy = scope.NeedsDedup ? "GROUP BY f.path" : "";
        var countExpr = scope.NeedsDedup ? "COUNT(DISTINCT f.path)" : "COUNT(*)";
        var sql = $"SELECT f.* FROM {from} WHERE {whereSql} {groupBy} ORDER BY {orderByColumn} {orderByDir} LIMIT @Limit OFFSET @Offset";
        var countSql = $"SELECT {countExpr} FROM {from} WHERE {whereSql}";

        using var connection = CreateConnection();
        var files = await connection.QueryAsync<IndexedFileDto>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        return new SearchResult
        {
            Files = files.Select(MapToIndexedFile).ToList(),
            TotalCount = total,
            SearchDuration = sw.Elapsed
        };
    }

    // Builds an FTS5 MATCH expression from raw user input.
    // The unicode61 tokenizer strips punctuation and splits on non-alphanumeric chars,
    // so "D&D" is indexed as two adjacent tokens "d" and "d". We replicate this splitting:
    // - Simple words like "animist" become quoted prefix searches: "animist"*
    //   (quoting stops FTS5 from reading AND/OR/NOT/NEAR as operators)
    // - Words with punctuation like "d&d" are split into sub-tokens ("d","d")
    //   and combined with NEAR(..., 0) to require them adjacent, matching the original text.
    // Tokens are letters/digits only, so they never contain a double quote to escape.
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
                // e.g. "anim" -> "anim"*  (matches "animist", "animation", etc.)
                ftsTerms.Add($"\"{tokens[0]}\"*");
            }
        }
        return string.Join(" ", ftsTerms);
    }

    public Task<SearchResult> SearchAsync(string query, int limit = 100, int offset = 0, IEnumerable<int>? collectionIds = null) =>
        SearchWithSortAsync(query, SortColumn.ModifiedAt, SortDirection.Desc, limit, offset, collectionIds);

    public Task<SearchResult> SearchByExtensionAsync(string extension, int limit = 100, int offset = 0, IEnumerable<int>? collectionIds = null)
    {
        var normalizedExt = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        return SearchWithSortAsync("", SortColumn.ModifiedAt, SortDirection.Desc, limit, offset, collectionIds, [normalizedExt]);
    }

    public async Task<IndexStats> GetStatsAsync(IEnumerable<int>? collectionIds = null)
    {
        var scope = CollectionScope.From(collectionIds);
        using var connection = CreateConnection();

        // When several collections are shown, a path indexed in more than one of them is counted
        // once (SQLite takes the bare columns from the row holding the MAX).
        var rows = scope.NeedsDedup
            ? $"(SELECT path, size_bytes, extension, MAX(indexed_at_utc) AS indexed_at_utc FROM files f WHERE {scope.Clause} GROUP BY path)"
            : $"(SELECT path, size_bytes, extension, indexed_at_utc FROM files f WHERE {scope.Clause})";

        var totals = await connection.QuerySingleAsync<(long Count, long Size, string? LastIndexed)>(
            $"SELECT COUNT(*), COALESCE(SUM(size_bytes), 0), MAX(indexed_at_utc) FROM {rows}");
        var extensions = await connection.QueryAsync<(string Extension, int Count)>(
            $"SELECT extension, COUNT(*) AS Count FROM {rows} GROUP BY extension ORDER BY Count DESC LIMIT 20");

        return new IndexStats
        {
            TotalFiles = (int)totals.Count,
            TotalSizeBytes = totals.Size,
            LastIndexedAtUtc = ParseUtcOrNull(totals.LastIndexed),
            FilesByExtension = extensions.ToDictionary(e => e.Extension, e => e.Count)
        };
    }

    public async Task ClearCollectionAsync(int collectionId)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });
    }

    // Path -> modified timestamp (round-trip string) of every row of a collection. Loaded once
    // per scan so incremental scans compare in memory instead of issuing one query per file.
    public async Task<Dictionary<string, string>> GetCollectionFileStampsAsync(int collectionId)
    {
        using var connection = CreateConnection();
        var rows = await connection.QueryAsync<(string Path, string Modified)>(
            "SELECT path, modified_at_utc FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });
        return rows.ToDictionary(r => r.Path, r => r.Modified, StringComparer.Ordinal);
    }

    public async Task<int> DeleteCollectionFilesByPathsAsync(int collectionId, IEnumerable<string> paths)
    {
        var deleted = 0;
        using var connection = CreateConnection();
        // Stay well under SQLite's bound-parameter limit.
        foreach (var chunk in paths.Chunk(500))
        {
            using var tx = connection.BeginTransaction();
            deleted += await connection.ExecuteAsync(
                "DELETE FROM files WHERE collection_id = @CollectionId AND path IN @Paths",
                new { CollectionId = collectionId, Paths = chunk }, tx);
            tx.Commit();
        }
        return deleted;
    }

    public static string FormatUtc(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseUtc(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static DateTime? ParseUtcOrNull(string? value) => string.IsNullOrEmpty(value) ? null : ParseUtc(value);

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

    // Rewrites every row (in all collections) for a file or folder that moved on disk from
    // oldPath to newPath, including all rows below it when it is a folder. Rows already indexed
    // at the destination are dropped first: they describe what the move just replaced.
    public async Task MovePathAsync(string oldPath, string newPath)
    {
        var oldPrefix = oldPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var newPrefix = newPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var newName = Path.GetFileName(newPath.TrimEnd(Path.DirectorySeparatorChar));
        var p = new
        {
            OldPath = oldPath,
            NewPath = newPath,
            OldPrefix = oldPrefix,
            OldPrefixLen = oldPrefix.Length,
            NewPrefix = newPrefix,
            NewPrefixLen = newPrefix.Length,
            NewName = newName,
            NewDirectory = Path.GetDirectoryName(newPath) ?? "",
            NewExtension = Path.GetExtension(newName).ToLowerInvariant()
        };

        using var connection = CreateConnection();
        using var tx = connection.BeginTransaction();
        await connection.ExecuteAsync(
            "DELETE FROM files WHERE path = @NewPath OR substr(path, 1, @NewPrefixLen) = @NewPrefix", p, tx);
        await connection.ExecuteAsync("""
            UPDATE files
            SET path = @NewPath, directory = @NewDirectory, name = @NewName,
                extension = CASE WHEN is_directory = 1 THEN '' ELSE @NewExtension END
            WHERE path = @OldPath
            """, p, tx);
        // Descendants: swap the old folder prefix for the new one in both path and directory.
        await connection.ExecuteAsync("""
            UPDATE files
            SET path = @NewPrefix || substr(path, @OldPrefixLen + 1),
                directory = CASE
                    WHEN directory = @OldPath THEN @NewPath
                    ELSE @NewPrefix || substr(directory, @OldPrefixLen + 1)
                END
            WHERE substr(path, 1, @OldPrefixLen) = @OldPrefix
            """, p, tx);
        tx.Commit();
    }

    // Removes every row (in all collections) for the given paths and anything below them.
    public async Task DeletePathsAsync(IEnumerable<string> paths)
    {
        using var connection = CreateConnection();
        using var tx = connection.BeginTransaction();
        foreach (var path in paths)
        {
            var prefix = path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            await connection.ExecuteAsync(
                "DELETE FROM files WHERE path = @Path OR substr(path, 1, @PrefixLen) = @Prefix",
                new { Path = path, Prefix = prefix, PrefixLen = prefix.Length }, tx);
        }
        tx.Commit();
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
    private const string CollectionSelect = """
        SELECT c.*,
               (SELECT COUNT(*) FROM files f WHERE f.collection_id = c.id) AS File_Count,
               (SELECT MAX(indexed_at_utc) FROM files f WHERE f.collection_id = c.id) AS Last_Indexed_At_Utc
        FROM collections c
        """;

    public Task<List<Collection>> GetCollectionsAsync() =>
        QueryCollectionsAsync($"{CollectionSelect} ORDER BY c.name", null);

    public async Task<Collection?> GetCollectionByIdAsync(int id) =>
        (await QueryCollectionsAsync($"{CollectionSelect} WHERE c.id = @Id", id)).SingleOrDefault();

    // Loads collections with their stats and paths in two queries instead of three per collection.
    private async Task<List<Collection>> QueryCollectionsAsync(string sql, int? id)
    {
        using var connection = CreateConnection();
        var dtos = (await connection.QueryAsync<CollectionDto>(sql, new { Id = id })).ToList();
        if (dtos.Count == 0) return new List<Collection>();

        var paths = (await connection.QueryAsync<CollectionPathDto>(
                "SELECT * FROM collection_paths WHERE collection_id IN @Ids ORDER BY id",
                new { Ids = dtos.Select(d => d.Id) }))
            .Select(MapToCollectionPath)
            .ToLookup(p => p.CollectionId);

        return dtos.Select(dto =>
        {
            var collection = MapToCollection(dto);
            collection.Paths = paths[collection.Id].ToList();
            collection.FileCount = (int)dto.File_Count;
            collection.LastIndexedAtUtc = ParseUtcOrNull(dto.Last_Indexed_At_Utc);
            return collection;
        }).ToList();
    }

    // Creates the collection and its paths atomically: an import never leaves a half-created one.
    public async Task<Collection> CreateCollectionAsync(string name, string? description, string excludedDirectories = "__MACOSX", IEnumerable<string>? paths = null)
    {
        var now = DateTime.UtcNow;
        using var connection = CreateConnection();
        using var tx = connection.BeginTransaction();
        var id = await connection.ExecuteScalarAsync<int>("""
            INSERT INTO collections (name, description, created_at_utc, excluded_directories)
            VALUES (@Name, @Description, @CreatedAtUtc, @ExcludedDirectories);
            SELECT last_insert_rowid();
            """, new { Name = name, Description = description, CreatedAtUtc = FormatUtc(now), ExcludedDirectories = excludedDirectories }, tx);

        var collection = new Collection
        {
            Id = id,
            Name = name,
            Description = description,
            ExcludedDirectories = excludedDirectories,
            CreatedAtUtc = now
        };

        foreach (var path in paths ?? [])
        {
            var pathId = await connection.ExecuteScalarAsync<int>(
                "INSERT INTO collection_paths (collection_id, path) VALUES (@CollectionId, @Path); SELECT last_insert_rowid();",
                new { CollectionId = id, Path = path }, tx);
            collection.Paths.Add(new CollectionPath { Id = pathId, CollectionId = id, Path = path });
        }

        tx.Commit();
        return collection;
    }

    public async Task UpdateCollectionAsync(int id, string name, string? description, string? excludedDirectories = null)
    {
        using var connection = CreateConnection();
        await connection.ExecuteAsync("""
            UPDATE collections
            SET name = @Name, description = @Description,
                excluded_directories = COALESCE(@ExcludedDirectories, excluded_directories)
            WHERE id = @Id
            """, new { Id = id, Name = name, Description = description, ExcludedDirectories = excludedDirectories });
    }

    public async Task DeleteCollectionAsync(int id)
    {
        using var connection = CreateConnection();
        // CASCADE (foreign_keys=ON) deletes collection_paths and files
        await connection.ExecuteAsync("DELETE FROM collections WHERE id = @Id", new { Id = id });
    }

    public async Task<List<CollectionPath>> GetCollectionPathsAsync(int collectionId)
    {
        using var connection = CreateConnection();
        var paths = await connection.QueryAsync<CollectionPathDto>(
            "SELECT * FROM collection_paths WHERE collection_id = @CollectionId ORDER BY id",
            new { CollectionId = collectionId });
        return paths.Select(MapToCollectionPath).ToList();
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
            if (PathHelper.IsSameOrUnder(normalizedNewPath, normalizedExisting))
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
            else if (PathHelper.IsSameOrUnder(normalizedExisting, normalizedNewPath))
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
        var stats = await connection.QuerySingleAsync<(long Count, string? LastIndexed)>(
            "SELECT COUNT(*), MAX(indexed_at_utc) FROM files WHERE collection_id = @CollectionId",
            new { CollectionId = collectionId });
        return ((int)stats.Count, ParseUtcOrNull(stats.LastIndexed));
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    // Collection filter shared by search and stats. Ids are ints, so inlining them is injection-safe.
    private readonly record struct CollectionScope(string Clause, bool NeedsDedup)
    {
        public static CollectionScope From(IEnumerable<int>? collectionIds)
        {
            var ids = collectionIds?.ToList();
            return ids is { Count: > 0 }
                ? new CollectionScope($"f.collection_id IN ({string.Join(",", ids)})", ids.Count > 1)
                : new CollectionScope("1=1", true);
        }
    }

    private static Collection MapToCollection(CollectionDto dto) => new()
    {
        Id = (int)dto.Id,
        Name = dto.Name,
        Description = dto.Description,
        ExcludedDirectories = dto.Excluded_Directories ?? "__MACOSX",
        CreatedAtUtc = ParseUtc(dto.Created_At_Utc)
    };

    private static CollectionPath MapToCollectionPath(CollectionPathDto p) => new()
    {
        Id = (int)p.Id,
        CollectionId = (int)p.Collection_Id,
        Path = p.Path
    };

    private class CollectionDto
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Created_At_Utc { get; set; } = "";
        public string Excluded_Directories { get; set; } = "__MACOSX";
        public long File_Count { get; set; }
        public string? Last_Indexed_At_Utc { get; set; }
    }

    private class CollectionPathDto
    {
        public long Id { get; set; }
        public long Collection_Id { get; set; }
        public string Path { get; set; } = "";
    }

    // DTO for Dapper mapping
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
        CreatedAtUtc = ParseUtc(dto.Created_At_Utc),
        ModifiedAtUtc = ParseUtc(dto.Modified_At_Utc),
        IndexedAtUtc = ParseUtc(dto.Indexed_At_Utc)
    };
}
