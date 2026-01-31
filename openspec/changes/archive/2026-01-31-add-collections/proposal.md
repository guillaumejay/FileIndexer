## Why

Currently, all indexed files live in a single flat index. Users with multiple NAS drives or distinct file categories (photos, documents, music) have no way to organize or selectively scan/filter their indexed content. Collections allow grouping paths for targeted indexing and filtering.

## What Changes

- **New Collections page**: CRUD interface to create, edit, and delete collections
- **Path management**: Each collection contains one or more root paths to index
- **Path overlap warnings**: When adding a path that overlaps with another collection, show a warning (but allow it)
- **Per-collection indexing**: Trigger indexing for a specific collection from the Collections page
- **Collection filter on Home page**: Multi-select filter to show results from one or more collections
- **Deduplicated results**: When multiple collections are selected (or all), deduplicate results by path
- **Schema changes**: New `collections` and `collection_paths` tables; `collection_id` FK on `files` table; path uniqueness constraint removed
- **BREAKING**: Existing indexed data will be wiped on migration

## Capabilities

### New Capabilities

- `collections`: Collection CRUD, path management, overlap detection, per-collection indexing, and search filtering

### Modified Capabilities

None. Existing specs (virtualized-file-list, indexation-modal, etc.) remain unchanged in their requirements.

## Impact

- **Database**: Schema migration required; existing data wiped
- **Data layer**: `IndexDbContext` needs new tables and modified queries
- **Scanner**: `FileScannerService` must accept collection context when indexing
- **Search**: Queries need collection filtering and deduplication logic
- **UI**: New Collections page; Home page gets multi-select collection filter
- **Navigation**: Add Collections link to nav menu
