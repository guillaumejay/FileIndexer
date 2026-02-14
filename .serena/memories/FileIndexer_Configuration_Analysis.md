# FileIndexer Configuration Export/Import Analysis

## Project Structure
- **src/FileIndexer.Core**: Shared models, data layer, and business logic
- **src/FileIndexer.Web**: ASP.NET Blazor Server web application
- **src/FileIndexer.Maui**: MAUI desktop/mobile app
- **src/FileIndexer.Desktop**: Shared desktop services for scanner, file operations, and trash

---

## 1. CONFIGURABLE STATE (What Can Be Exported/Imported)

### A. Application Settings (src/FileIndexer.Web/AppSettings.cs)
**File**: `src/FileIndexer.Web/AppSettings.cs` and `appsettings.json`

Properties (system configuration):
- `DefaultScanPath` (string): Pre-filled path in UI for collections - default "R:\\JDR"
- `DatabasePath` (string): SQLite database file location - default "fileindex.db"
- `ScanParallelism` (int): Parallel threads for scanning - default 64
- `ScanBatchSize` (int): Batch size for DB inserts - default 500

**These are app-level settings loaded from `appsettings.json` and injected as singletons.**

### B. Collections & Paths (Database Schema)
**Files**: 
- `src/FileIndexer.Core/Models/Collection.cs`
- `src/FileIndexer.Core/Data/IndexDbContext.cs` (tables: collections, collection_paths)

**Collection Model**:
```csharp
public class Collection
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<CollectionPath> Paths { get; set; } = new();
    public int FileCount { get; set; }         // Stats only
    public DateTime? LastIndexedAtUtc { get; set; }  // Stats only
}

public class CollectionPath
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public required string Path { get; set; }
}
```

**Database Tables**:
- `collections`: id, name (UNIQUE), description, created_at_utc
- `collection_paths`: id, collection_id (FK), path
- `files`: id, collection_id (FK), name, path, directory, extension, size_bytes, created_at_utc, modified_at_utc, indexed_at_utc
- `files_fts`: FTS5 virtual table for full-text search (name, path, directory)

### C. MAUI App Settings (Preferences)
**File**: `src/FileIndexer.Maui/Services/DatabasePathService.cs`

Stores one preference key:
- `"DatabasePath"` (string): Path to the SQLite database file

Uses MAUI's `Preferences` API (platform-specific storage on each OS):
- Windows: Registry
- macOS: UserDefaults
- iOS: Keychain/NSUserDefaults
- Android: SharedPreferences

---

## 2. DATABASE SCHEMA DETAILS

### SQLite Tables

**collections table**:
```sql
CREATE TABLE collections (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    created_at_utc TEXT NOT NULL
)
```

**collection_paths table**:
```sql
CREATE TABLE collection_paths (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    collection_id INTEGER NOT NULL,
    path TEXT NOT NULL,
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE
)
```

**files table** (indexed file metadata):
```sql
CREATE TABLE files (
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
```

**files_fts table** (FTS5 virtual table for search):
```sql
CREATE VIRTUAL TABLE files_fts USING fts5(
    name, path, directory,
    content='files',
    content_rowid='id',
    tokenize='unicode61 remove_diacritics 2'
)
```

**Key DB Constraints**:
- Collection names are UNIQUE
- Foreign key cascades on delete
- Triggers keep FTS5 index synchronized automatically

---

## 3. HOW SETTINGS ARE LOADED

### Web App (Program.cs)
```csharp
// Line 10: Load from appsettings.json
var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new AppSettings();

// Line 16-17: Registered as singleton
builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton(sp => new IndexDbContext(appSettings.DatabasePath));
```

### MAUI App (MauiProgram.cs)
```csharp
// Line 34: DatabasePathService registered as singleton
builder.Services.AddSingleton<DatabasePathService>();

// Lines 37-42: Database path comes from DatabasePathService (which reads Preferences)
builder.Services.AddSingleton<IndexDbContext>(sp =>
{
    var dbPathService = sp.GetRequiredService<DatabasePathService>();
    var dbPath = dbPathService.GetDatabasePath();
    return new IndexDbContext(dbPath ?? ":memory:");
});
```

---

## 4. WHAT CONSTITUTES "USER CONFIGURATION" FOR EXPORT/IMPORT

### Primary Configuration Items:
1. **Collection Definitions** (from `collections` table)
   - Collection name (UNIQUE constraint - important!)
   - Collection description
   - Created date/time
   
2. **Collection Paths** (from `collection_paths` table)
   - Folder paths to index
   - Associated collection IDs
   
3. **App Settings** (from `appsettings.json`)
   - DefaultScanPath
   - DatabasePath
   - ScanParallelism
   - ScanBatchSize

### NOT Included in Configuration (indexed data):
- `files` table data (indexed files) - this is generated by scanning
- `files_fts` table data - this is generated from files
- FileCount and LastIndexedAtUtc are stats, not config

---

## 5. SERIALIZATION PATTERNS IN CODEBASE

### Current Serialization:
- **Collections**: Loaded/saved via IndexDbContext methods using Dapper SQL
- **App Settings**: JSON via `Configuration.GetSection().Get<AppSettings>()`
- **MAUI Preferences**: Using MAUI's `Preferences` API (not exposed as JSON)

### Methods for Collection CRUD:
- `GetCollectionsAsync()`: Returns full collection objects with paths
- `CreateCollectionAsync(name, description)`: Creates and returns collection
- `UpdateCollectionAsync(id, name, description)`: Updates collection
- `DeleteCollectionAsync(id)`: Deletes collection (cascades to paths and files)
- `AddCollectionPathAsync(collectionId, path)`: Adds path to collection
- `RemoveCollectionPathAsync(pathId)`: Removes path from collection

### DateTime Handling:
- All dates stored as ISO 8601 strings (format "O")
- Example: `DateTime.UtcNow.ToString("O")` → "2025-02-11T14:30:45.1234567Z"
- Parsed back with: `DateTime.Parse(dateString)`

---

## 6. KEY FILES SUMMARY

### Core Models & Data:
- `/d/github/FileIndexer/src/FileIndexer.Core/Models/Collection.cs`: Collection and CollectionPath models
- `/d/github/FileIndexer/src/FileIndexer.Core/Models/IndexedFile.cs`: IndexedFile, SearchResult, IndexStats models
- `/d/github/FileIndexer/src/FileIndexer.Core/Data/IndexDbContext.cs`: Database schema and all CRUD operations

### Configuration:
- `/d/github/FileIndexer/src/FileIndexer.Web/AppSettings.cs`: AppSettings POCO
- `/d/github/FileIndexer/src/FileIndexer.Web/appsettings.json`: Actual app settings values
- `/d/github/FileIndexer/src/FileIndexer.Maui/Services/DatabasePathService.cs`: MAUI preferences wrapper

### Services:
- `/d/github/FileIndexer/src/FileIndexer.Core/Services/CollectionService.cs`: Wrapper around IndexDbContext for collections
- `/d/github/FileIndexer/src/FileIndexer.Core/Services/SearchService.cs`: Search operations
- `/d/github/FileIndexer/src/FileIndexer.Web/Program.cs`: Web app dependency injection
- `/d/github/FileIndexer/src/FileIndexer.Maui/MauiProgram.cs`: MAUI app dependency injection

---

## 7. EXPORT FORMAT RECOMMENDATIONS

For configuration export, a JSON structure like this would be appropriate:

```json
{
  "version": "1.0",
  "exportDate": "2025-02-11T14:30:45Z",
  "appSettings": {
    "defaultScanPath": "R:\\JDR",
    "databasePath": "fileindex.db",
    "scanParallelism": 32,
    "scanBatchSize": 500
  },
  "collections": [
    {
      "id": 1,
      "name": "My Documents",
      "description": "Important documents",
      "createdAtUtc": "2025-02-10T10:00:00Z",
      "paths": [
        {
          "id": 1,
          "path": "C:\\Users\\John\\Documents"
        }
      ]
    }
  ]
}
```

---

## 8. CONSIDERATIONS FOR IMPLEMENTATION

1. **Collection Name Uniqueness**: The `name` column has UNIQUE constraint in DB
   - On import, handle conflicts (rename, skip, or ask user)
   
2. **Path Resolution**: Collection paths may not exist on target system
   - Validate/prompt user for path adjustments on import
   
3. **Database Selection**: 
   - MAUI stores DB path in Preferences (per-app, per-platform)
   - Web app uses appsettings.json
   - Decide if importing should also update database location
   
4. **Foreign Keys**: 
   - Collection IDs may change after import
   - Consider re-assigning IDs to avoid conflicts
   
5. **Indexed Data**: 
   - Files table is NOT user config, only collections+paths
   - Allow choosing to preserve or clear indexed files on import
