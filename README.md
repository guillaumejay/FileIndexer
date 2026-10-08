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
  "AllowedHosts": "localhost;127.0.0.1;[::1]",
  "AppSettings": {
    "DatabasePath": "fileindex.db",
    "ScanParallelism": 32,
    "ScanBatchSize": 500
  }
}
```

### Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `DatabasePath` | SQLite database location (relative paths are resolved against the app folder) | `fileindex.db` |
| `ScanParallelism` | Number of folders scanned in parallel | 64 |
| `ScanBatchSize` | Batch size for DB writes | 500 |

> ⚠️ The Web app has **no authentication** and can move, delete and open files on the machine it
> runs on. It only accepts `localhost` requests by default (`AllowedHosts`). Only widen that, or
> bind Kestrel to another interface, on a network you fully trust.

## Architecture

The project is divided into several layers to maximize code reuse:

- **src/FileIndexer.Core**: Shared logic, SQLite FTS5 access, and data models.
- **src/FileIndexer.Desktop**: File system services (scanner, file operations, archives, trash).
- **src/FileIndexer.UI**: Razor Class Library with every Blazor component, the CSS and the JS,
  shared by both hosts. Host differences go through `PlatformCapabilities` (file system access,
  touch) and optional host services (`INativeFolderPicker`, `IConfigFileExchange`).
- **src/FileIndexer.Web**: Blazor Server host (configuration, `Program.cs`, one page).
- **src/FileIndexer.Maui**: .NET MAUI Hybrid host (database selection, native dialogs). On
  Android/iOS it is a read-only browser of a synced index: the database is copied into app storage.

```
FileIndexer/
├── src/
│   ├── FileIndexer.Core/      # Data Layer & Services
│   ├── FileIndexer.Desktop/   # File system services
│   ├── FileIndexer.UI/        # Shared Blazor components, CSS, JS
│   ├── FileIndexer.Web/       # Web Host
│   └── FileIndexer.Maui/      # Native Host (Hybrid)
├── tests/                     # xunit v3 + bUnit (`dotnet test`)
├── openspec/                  # Specification-driven development artifacts
└── agents.md                  # Specialized AI Agent roles
```

## GitHub Releases

This project uses GitHub Actions for automated releases. Every push to the `main` branch triggers a new release with:
- **Web**: Windows and Linux standalone packages.
- **Android**: APK for mobile devices.

Versions are tagged using the format `vYYYY.MM.DD.HHmm`.

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
