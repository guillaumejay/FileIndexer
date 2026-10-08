# AI Agents for FileIndexer

This file defines the specialized AI agent roles and responsibilities for the FileIndexer project. Use these roles to guide task delegation and ensure domain-specific expertise is applied.

## 🏗️ Backend/Core Specialist
**Focus:** `FileIndexer.Core`
- **Domain:** SQLite, FTS5, Dapper, .NET 10 performance, file system scanning.
- **Responsibilities:**
  - Maintain and optimize `IndexDbContext.cs` and raw SQL queries.
  - Enhance `FileScannerService.cs` (parallelism, channels, producer-consumer).
  - Manage data models in `Models/`.
  - Ensure high-performance search in `SearchService.cs`.

## 🌐 Web/Frontend Specialist
**Focus:** `FileIndexer.UI` (shared components) & `FileIndexer.Web`
- **Domain:** Blazor Server, Interactive SSR, CSS, UI/UX.
- **Responsibilities:**
  - Develop and maintain the shared Blazor components in `src/FileIndexer.UI/Components/` (used by both hosts; never duplicate them in a host).
  - Ensure real-time UI updates for scan progress.
  - Implement responsive design and modern aesthetics.
  - Maintain `AppSettings.cs` and `appsettings.json` configurations.

## 📱 Maui/Mobile Specialist
**Focus:** `FileIndexer.Maui` & `FileIndexer.Desktop`
- **Domain:** .NET MAUI, cross-platform UI, platform-native services.
- **Responsibilities:**
  - Keep host-specific code thin: platform differences go through `PlatformCapabilities` and host services (`INativeFolderPicker`, `IConfigFileExchange`).
  - Implement platform-specific logic (e.g., `WindowsTrashService`, `LinuxTrashService`).
  - Optimize the mobile touch-friendly UI.
  - Manage multi-project shared services and data models.

## 📜 OpenSpec/Workflow Specialist
**Focus:** `openspec/`
- **Domain:** Spec-driven development, artifact management, change tracking.
- **Responsibilities:**
  - Manage `proposals`, `designs`, `tasks`, and `specs` in `openspec/`.
  - Ensure all implementation work follows the OpenSpec workflow.
  - Keep main specs synchronized with changes.
  - Validate that implementation matches documented requirements.

## 📚 Documentation & QA Specialist
**Focus:** Project-wide
- **Domain:** `README.md`, `CLAUDE.md`, project consistency, logging.
- **Responsibilities:**
  - Maintain project documentation and build instructions.
  - Ensure consistent English comments and logging.
  - Verify overall project health and cross-project consistency (Web/MAUI sync).
  - Guide onboarding for new contributors or tools.
