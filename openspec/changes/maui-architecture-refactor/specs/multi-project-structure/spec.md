## ADDED Requirements

### Requirement: Solution contains four projects
The solution SHALL contain exactly four projects:
- `FileIndexer.Core` - shared library
- `FileIndexer.Desktop` - desktop-only services library
- `FileIndexer.Web` - Blazor Server application
- `FileIndexer.Maui` - MAUI Blazor Hybrid application

#### Scenario: Solution structure
- **WHEN** opening FileIndexer.sln
- **THEN** all four projects are present and buildable

### Requirement: Core project contains shared code
FileIndexer.Core SHALL contain:
- All model classes (IndexedFile, Collection)
- IndexDbContext for database access
- SearchService for search operations
- CollectionService for collection read operations

#### Scenario: Core project dependencies
- **WHEN** building FileIndexer.Core
- **THEN** it has no dependency on Desktop, Web, or Maui projects

#### Scenario: Core models available everywhere
- **WHEN** referencing FileIndexer.Core
- **THEN** IndexedFile and Collection models are accessible

### Requirement: Desktop project contains platform-specific services
FileIndexer.Desktop SHALL contain:
- FileScannerService for directory scanning
- FileOperationsService for file operations (open, rename, copy, move)
- ITrashService and platform implementations (Windows, Linux, Mac)

#### Scenario: Desktop depends only on Core
- **WHEN** building FileIndexer.Desktop
- **THEN** it references FileIndexer.Core only

#### Scenario: Desktop services isolated
- **WHEN** FileIndexer.Maui is built
- **THEN** it does NOT reference FileIndexer.Desktop

### Requirement: Web project references Core and Desktop
FileIndexer.Web SHALL reference both FileIndexer.Core and FileIndexer.Desktop to provide full functionality.

#### Scenario: Web has full functionality
- **WHEN** running FileIndexer.Web
- **THEN** scanning, searching, and file operations all work as before

### Requirement: Project targeting compatibility
- FileIndexer.Core SHALL target `net10.0`
- FileIndexer.Desktop SHALL target `net10.0`
- FileIndexer.Web SHALL target `net10.0`
- FileIndexer.Maui SHALL target `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, `net10.0-windows10.0.19041.0`

#### Scenario: All projects build successfully
- **WHEN** running `dotnet build` on the solution
- **THEN** all projects compile without errors
