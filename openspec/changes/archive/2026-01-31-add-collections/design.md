## Context

FileIndexer currently indexes files into a single flat database. All files go into one `files` table, and the scanner (`FileScannerService.ScanAsync`) takes a single root path and clears all data before re-indexing.

Users want to organize indexed content into named collections, each with its own set of root paths, and filter search results by collection.

Key constraints:
- SQLite + Dapper (no EF), singleton `IndexDbContext`
- Blazor Server with interactive SSR
- Scanner uses channels and parallel processing
- Existing data will be wiped on migration

## Goals / Non-Goals

**Goals:**
- Collections CRUD with path management
- Per-collection indexing (scan only one collection's paths)
- Multi-select collection filter on search
- Path overlap warnings (informational, not blocking)

**Non-Goals:**
- Scheduled/automatic indexing
- Collection sharing or permissions
- Nested collections or hierarchies
- Preserving existing indexed data through migration

## Decisions

### 1. Schema Design

```sql
-- New tables
CREATE TABLE collections (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    created_at_utc TEXT NOT NULL
);

CREATE TABLE collection_paths (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    collection_id INTEGER NOT NULL,
    path TEXT NOT NULL,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
);

-- Modified files table (path no longer unique)
ALTER TABLE files ADD COLUMN collection_id INTEGER
    REFERENCES collections(id) ON DELETE CASCADE;
```

**Rationale**: Simple FK on files rather than join table. Files can be duplicated across collections (same path, different collection_id). This matches user expectation that each collection is self-contained.

**Alternative considered**: Join table for many-to-many. Rejected because it adds query complexity and the user confirmed collections won't meaningfully overlap in practice.

### 2. Deduplication Strategy

When filtering by multiple collections or "All", deduplicate by path using `GROUP BY path` with arbitrary row selection:

```sql
SELECT * FROM files
WHERE collection_id IN (@ids)
GROUP BY path
ORDER BY name
LIMIT @limit OFFSET @offset
```

**Rationale**: Simple, fast, deterministic. User doesn't see which collection a file came from (by design - no collection badge on results).

**Alternative considered**: `DISTINCT ON` or window functions. SQLite doesn't support `DISTINCT ON`; window functions add complexity for same result.

### 3. Scanner Modifications

`FileScannerService.ScanAsync` signature changes:

```csharp
// Current
Task<ScanProgress> ScanAsync(string rootPath, bool incrementalScan = false)

// New
Task<ScanProgress> ScanAsync(int collectionId, bool incrementalScan = false)
```

The scanner will:
1. Look up paths for the given collection
2. Clear only files with that `collection_id` (not all files)
3. Scan all paths, tagging each file with `collection_id`

**Rationale**: Collection-scoped clearing allows re-indexing one collection without affecting others.

### 4. FTS Index Updates

The FTS5 triggers already handle insert/update/delete. No changes needed - FTS stays synchronized automatically when files are inserted with collection_id.

### 5. Path Overlap Detection

Check overlaps in application code when adding a path:

```csharp
// Pseudo-code
var existingPaths = await GetAllCollectionPaths();
var overlaps = existingPaths
    .Where(p => newPath.StartsWith(p.Path) || p.Path.StartsWith(newPath))
    .Where(p => p.CollectionId != currentCollectionId);
```

**Rationale**: Simple string prefix matching. No need for filesystem traversal - we're just warning, not blocking.

### 6. UI Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `Home.razor` | `/` | Container with tab navigation (Search / Collections) |
| `SearchView.razor` | Tab content | Search UI with collection filter chips |
| `CollectionsView.razor` | Tab content | CRUD with card-based layout |
| `CollectionEditor.razor` | Modal | Create/edit form with path list |
| `FolderBrowser.razor` | Modal | System folder browser for path selection |

**Rationale**: Tab-based navigation within a single page eliminates theme flash that occurred when navigating between separate pages (theme state is managed once in `Home.razor`). Modal for editing follows existing patterns.

### 7. Service Layer

New `CollectionService` (scoped):
- `GetAllAsync()`, `GetByIdAsync(int id)`
- `CreateAsync(Collection c)`, `UpdateAsync(Collection c)`, `DeleteAsync(int id)`
- `GetPathsAsync(int collectionId)`, `AddPathAsync(int collectionId, string path)`, `RemovePathAsync(int pathId)`
- `CheckPathOverlapsAsync(int collectionId, string path)`
- `GetStatsAsync(int collectionId)` - file count, last indexed time

**Rationale**: Separate from `IndexDbContext` to keep concerns clean. Scoped lifetime like `SearchService`.

## Risks / Trade-offs

**[Data duplication]** → Accept it. Same file can exist in multiple rows if paths overlap. Deduplication happens at query time.

**[Path validation on Windows vs Linux]** → Validate path exists at add time. Store paths as-is (no normalization). User's responsibility to use correct path format for their OS.

**[Large collection scans blocking UI]** → Existing progress events (`OnProgressChanged`) already handle this. No additional work needed.

**[Breaking change - data wipe]** → Clear communication in UI. Could add a migration warning on first load, but likely overkill for current user base.

## Migration Plan

1. Drop existing `files` and `files_fts` tables
2. Recreate `files` with `collection_id` column (no UNIQUE on path)
3. Create `collections` and `collection_paths` tables
4. Recreate FTS5 table and triggers
5. No data migration - users re-index into collections

Rollback: Restore from backup or re-run original schema creation. No automated rollback needed given clean-slate approach.
