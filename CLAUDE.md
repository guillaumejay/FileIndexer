# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FileIndexer is a high-performance file indexing application for NAS drives with a Blazor Server web interface. It scans directories in parallel and provides fast full-text search using SQLite FTS5.

## Build and Run Commands

```bash
# Restore dependencies
dotnet restore

# Run the application (accessible at http://localhost:5000)
dotnet run

# Build for production
dotnet build -c Release

# Publish for specific platforms
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/win
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux
```

## Architecture

### Technology Stack
- .NET 10, Blazor Server with interactive server-side rendering
- SQLite with FTS5 for full-text search via Microsoft.Data.Sqlite + Dapper
- No Entity Framework - uses raw SQL with Dapper for performance

### Key Components

**Data Layer** (`Data/IndexDbContext.cs`):
- Manages SQLite connection and schema initialization
- `files` table stores file metadata; `files_fts` is the FTS5 virtual table
- Triggers keep FTS index synchronized automatically
- Bulk insert with transactions for scan performance

**Scanner** (`Services/FileScannerService.cs`):
- Two-phase scan: enumerate directories first, then scan files in parallel
- Uses `System.Threading.Channels` for producer-consumer pattern with back-pressure
- `Parallel.ForEachAsync` with configurable parallelism (default 64 threads)
- Supports incremental scanning (skips unchanged files)
- Exposes `OnProgressChanged` event for real-time UI updates

**Search** (`Services/SearchService.cs`):
- Thin wrapper around `IndexDbContext` search methods
- FTS5 prefix matching (query terms get `*` suffix automatically)

**Configuration** (`AppSettings.cs` + `appsettings.json`):
- `DefaultScanPath`: Pre-filled path in UI
- `DatabasePath`: SQLite file location (default: `fileindex.db`)
- `ScanParallelism`: Parallel threads for scanning
- `ScanBatchSize`: Batch size for DB inserts

### Service Lifetimes
- `IndexDbContext`: Singleton (single SQLite connection)
- `FileScannerService`: Singleton (maintains scan state)
- `SearchService`: Scoped (per-request)

## Code Patterns

- Models in `Models/IndexedFile.cs` include DTOs for search results, stats, and scan progress
- Blazor components in `Components/Pages/` - main UI is `Home.razor`
- English comments and log messages throughout the codebase
