## MODIFIED Requirements

### Requirement: Solution contains five projects
The solution SHALL contain these projects (plus the test project):
- `FileIndexer.Core` - shared library
- `FileIndexer.Desktop` - file system services library
- `FileIndexer.UI` - Razor Class Library with every Blazor component, the CSS and the JS
- `FileIndexer.Web` - Blazor Server host
- `FileIndexer.Maui` - MAUI Blazor Hybrid host

#### Scenario: Solution structure
- **WHEN** opening FileIndexer.sln
- **THEN** all projects are present and buildable

### Requirement: Hosts do not duplicate UI
Blazor components, styles and scripts SHALL live in `FileIndexer.UI` only. Hosts SHALL contain configuration, a routable page rendering `AppShell`, and host-specific services or settings.

#### Scenario: Fix once, both hosts
- **WHEN** a component of `FileIndexer.UI` changes
- **THEN** the Web and MAUI hosts both get the change without host edits

### Requirement: Desktop features are gated at runtime
`FileIndexer.UI` SHALL reference `FileIndexer.Desktop`. The desktop services SHALL be registered only when the host declares `PlatformCapabilities.HasFileSystemAccess`, and the components SHALL hide indexing and file operations otherwise.

#### Scenario: Mobile host
- **WHEN** FileIndexer.Maui runs on Android or iOS
- **THEN** collections are read-only and no desktop service is registered

#### Scenario: Desktop host
- **WHEN** FileIndexer.Web or FileIndexer.Maui runs on Windows or macOS
- **THEN** scanning, file operations and the activity log are available
