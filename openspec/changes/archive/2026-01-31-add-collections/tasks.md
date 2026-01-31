## 1. Database Schema

- [x] 1.1 Create `Collection` and `CollectionPath` model classes in `Models/`
- [x] 1.2 Update `IndexedFile` model to include `CollectionId` property
- [x] 1.3 Update `IndexDbContext.InitializeDatabase` to create `collections` and `collection_paths` tables
- [x] 1.4 Modify `files` table creation to include `collection_id` FK and remove UNIQUE constraint on path
- [x] 1.5 Update FTS5 triggers to handle collection-scoped operations

## 2. Collection Service

- [x] 2.1 Create `CollectionService` class in `Services/`
- [x] 2.2 Implement `GetAllAsync()` and `GetByIdAsync(int id)`
- [x] 2.3 Implement `CreateAsync()`, `UpdateAsync()`, `DeleteAsync()`
- [x] 2.4 Implement `GetPathsAsync()`, `AddPathAsync()`, `RemovePathAsync()`
- [x] 2.5 Implement `CheckPathOverlapsAsync()` for overlap detection
- [x] 2.6 Implement `GetStatsAsync()` for collection statistics (file count, last indexed)
- [x] 2.7 Register `CollectionService` as scoped in `Program.cs`

## 3. Scanner Updates

- [x] 3.1 Change `FileScannerService.ScanAsync` signature to accept `collectionId` instead of `rootPath`
- [x] 3.2 Add method to retrieve paths for a collection before scanning
- [x] 3.3 Modify `ClearAsync` in `IndexDbContext` to accept optional `collectionId` parameter
- [x] 3.4 Update `InsertFilesAsync` and `BulkInsertAsync` to include `collection_id`
- [x] 3.5 Update `ProduceFilesAsync` to scan multiple paths and tag files with collection

## 4. Search Updates

- [x] 4.1 Add `collectionIds` parameter to `SearchWithSortAsync`
- [x] 4.2 Implement deduplication with `GROUP BY path` when multiple collections selected
- [x] 4.3 Update `SearchAsync` and `SearchByExtensionAsync` to support collection filtering
- [x] 4.4 Update `GetStatsAsync` to support optional collection filtering

## 5. Collections Page UI

- [x] 5.1 Create `Collections.razor` page at `/collections`
- [x] 5.2 Add navigation link to Collections page in `NavMenu.razor`
- [x] 5.3 Implement collection list display with card-based layout
- [x] 5.4 Show collection stats (file count, last indexed timestamp, paths)
- [x] 5.5 Create `CollectionEditor.razor` modal component for create/edit
- [x] 5.6 Implement path list management in editor (add/remove paths)
- [x] 5.7 Implement path validation (check directory exists)
- [x] 5.8 Implement overlap warning display when adding paths
- [x] 5.9 Add Index button per collection to trigger scanning
- [x] 5.10 Add Delete button with confirmation for collections

## 6. Home Page Filter

- [x] 6.1 Add collection multi-select filter component to `Home.razor`
- [x] 6.2 Load available collections on page init
- [x] 6.3 Pass selected collection IDs to search service
- [x] 6.4 Update search results display to work with filtered/deduplicated results

## 7. Integration & Testing

- [x] 7.1 Test collection CRUD operations
- [x] 7.2 Test path overlap detection with parent/child directories
- [x] 7.3 Test per-collection indexing (verify only that collection's files are cleared/rescanned)
- [x] 7.4 Test multi-collection search with deduplication
- [x] 7.5 Test edge case: delete collection with indexed files
