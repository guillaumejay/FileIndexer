# File Indexer

High-performance file indexer for NAS drives with multiple interfaces (Web, Desktop, Mobile).

## Features

- **Parallel scanning**: Index 200k+ files in minutes using `System.Threading.Channels`
- **Instant search**: SQLite FTS5 with < 50ms response time
- **Cross-platform**: Windows, Linux, macOS, Android, iOS
- **Multiple Interfaces**: 
  - **Web**: Blazor Server for remote access
  - **Mobile/Desktop**: .NET MAUI Hybrid for native experience
- **Collections**: Group indexed files into logical collections

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- MAUI Workload (for Mobile/Desktop): `dotnet workload install maui`

## Getting Started

```bash
# Clone the project
git clone https://github.com/guill/FileIndexer.git
cd FileIndexer

# Restore all dependencies
dotnet restore
```

## Running the Application

### 🌐 Web Interface (Blazor Server)
The web version is ideal for NAS devices where you want to access the indexer via a browser.

```bash
dotnet run --project src/FileIndexer.Web
```
*Accessible at http://localhost:5000*

### 📱 Native Application (.NET MAUI)
The MAUI version provides a native experience with platform-specific features like "Move to Trash".

#### Windows
```bash
dotnet build -t:Run -f net10.0-windows10.0.19041.0 src/FileIndexer.Maui
```

#### macOS (Mac Catalyst)
```bash
dotnet build -t:Run -f net10.0-maccatalyst src/FileIndexer.Maui
```

#### Android
```bash
dotnet build -t:Run -f net10.0-android src/FileIndexer.Maui
```

## Configuration

Edit `appsettings.json` (Web) or use the in-app settings (MAUI):

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

## Architecture

The project is divided into several layers to maximize code reuse:

- **src/FileIndexer.Core**: Shared logic, SQLite FTS5 access, and data models.
- **src/FileIndexer.Desktop**: Shared desktop-specific logic (Windows/macOS features).
- **src/FileIndexer.Web**: Blazor Server web application.
- **src/FileIndexer.Maui**: .NET MAUI Hybrid application sharing the same Blazor components for the UI.

```
FileIndexer/
├── src/
│   ├── FileIndexer.Core/      # Data Layer & Services
│   ├── FileIndexer.Desktop/   # OS-specific services
│   ├── FileIndexer.Web/       # Web Host
│   └── FileIndexer.Maui/      # Native Host (Hybrid)
├── openspec/                  # Specification-driven development artifacts
└── agents.md                  # Specialized AI Agent roles
```

## Performance

| Volume | Scan time* | Search time |
|--------|-----------|-------------|
| 50k files | ~2 min | < 20ms |
| 200k files | ~8 min | < 50ms |
| 500k files | ~20 min | < 100ms |

*\*Depends on NAS network latency and IOPS.*

## Publishing (Web)

```bash
# Windows
dotnet publish src/FileIndexer.Web -c Release -r win-x64 --self-contained -o ./publish/win

# Linux
dotnet publish src/FileIndexer.Web -c Release -r linux-x64 --self-contained -o ./publish/linux
```

## Specialized AI Agents

Refer to [agents.md](./agents.md) for detailed roles and responsibilities when working on this project with AI assistants.

## License

MIT
