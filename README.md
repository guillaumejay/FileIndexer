# File Indexer

High-performance file indexer for NAS drives with web interface.

## Features

- **Parallel scanning**: Index 200k+ files in minutes
- **Instant search**: FTS5 with < 50ms response time
- **Cross-platform**: Windows, Linux, macOS
- **Modern web interface**: Blazor Server with real-time progress

## Prerequisites

- .NET 10 SDK

## Installation

```bash
# Clone/copy the project
cd FileIndexer

# Restore packages
dotnet restore

# Run the application
dotnet run
```

The application will be available at http://localhost:5000

## Configuration

Edit `appsettings.json`:

```json
{
  "AppSettings": {
    "DefaultScanPath": "/path/to/nas",
    "DatabasePath": "fileindex.db",
    "ScanParallelism": 64,
    "ScanBatchSize": 500
  }
}
```

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `DefaultScanPath` | Pre-filled path in the interface | empty |
| `DatabasePath` | SQLite database location | `fileindex.db` |
| `ScanParallelism` | Number of parallel threads | 64 |
| `ScanBatchSize` | Batch size for DB inserts | 500 |

## Supported Paths

### Windows
```
C:\Users\...
\\server\share
Z:\nas-mount
```

### Linux / macOS
```
/mnt/nas
/media/share
/Volumes/NAS
```

## Usage

1. **Configure the path**: Enter the NAS path in the text field
2. **Start the scan**: Click "Start scan"
3. **Search**: Use the search bar (real-time search)

### Search

- Search by filename with prefix matching (e.g., `report` finds `report-2024.pdf`)
- Click on an extension in the stats to filter
- Click on a row to copy the path

## Architecture

```
FileIndexer/
├── Models/              # Data models
├── Data/                # SQLite + FTS5 access
├── Services/            # Scanner + Search
├── Components/          # Blazor interface
│   ├── Layout/
│   └── Pages/
└── wwwroot/css/         # Styles
```

## Performance

| Volume | Scan time* | Search time |
|--------|-----------|-------------|
| 50k files | ~2 min | < 20ms |
| 200k files | ~8 min | < 50ms |
| 500k files | ~20 min | < 100ms |

*Depends on NAS network latency

## Publishing

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained -o ./publish/win

# Linux
dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux

# macOS (Intel)
dotnet publish -c Release -r osx-x64 --self-contained -o ./publish/osx

# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm
```

## License

MIT
