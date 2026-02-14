## ADDED Requirements

### Requirement: MAUI app is read-only
The MAUI application SHALL provide read-only access to the file index. It SHALL NOT include:
- File scanning functionality
- File rename, copy, move, or delete operations
- Any write operations to the database

#### Scenario: No modification actions visible
- **WHEN** viewing search results in the MAUI app
- **THEN** no context menu with modification actions is displayed

#### Scenario: No scan button visible
- **WHEN** viewing the MAUI app interface
- **THEN** no indexation/scan button is visible

### Requirement: Database file selection
The MAUI app SHALL allow users to select the SQLite database file from their device storage (synced via OneDrive/Dropbox).

#### Scenario: First launch configuration
- **WHEN** launching the app for the first time
- **THEN** user is prompted to select the .db file location

#### Scenario: Database path persistence
- **WHEN** user selects a .db file
- **THEN** the path is saved in app preferences and used on next launch

#### Scenario: Change database file
- **WHEN** user wants to change the .db file
- **THEN** a settings option allows selecting a new file

### Requirement: Search functionality
The MAUI app SHALL provide search functionality identical to the Web version:
- Full-text search in file names
- Sorting by name, directory, extension, size, date
- Collection filtering (if collections exist in the database)

#### Scenario: Search works on mobile
- **WHEN** user enters a search query
- **THEN** matching files are displayed with name, directory, extension, size, and date

#### Scenario: Sort columns
- **WHEN** user taps a column header
- **THEN** results are sorted by that column

### Requirement: Copy path to clipboard
The MAUI app SHALL allow users to copy a file's full path to the clipboard.

#### Scenario: Long press to copy path
- **WHEN** user long-presses on a file row
- **THEN** the file's full path is copied to clipboard
- **AND** a toast notification confirms the copy

### Requirement: Mobile-optimized UI
The MAUI app UI SHALL be optimized for mobile touch interaction:
- Larger touch targets than desktop
- Simplified layout for smaller screens
- No hover states (touch-only)

#### Scenario: Touch-friendly row height
- **WHEN** viewing search results on mobile
- **THEN** row height is at least 44px for comfortable tapping

### Requirement: Platform support
The MAUI app SHALL support:
- Android (minimum API 21 / Android 5.0)
- iOS (minimum iOS 14)
- macOS via Mac Catalyst
- Windows via WinUI 3

#### Scenario: Android build
- **WHEN** building for Android
- **THEN** an APK/AAB is produced

#### Scenario: iOS build
- **WHEN** building for iOS
- **THEN** an IPA is produced (requires Mac build agent)
