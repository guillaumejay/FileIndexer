## ADDED Requirements

### Requirement: MCP server over an existing index
The solution SHALL provide `FileIndexer.Mcp`, a stdio MCP server that opens an existing database given by `--db` and refuses to start when the file does not exist.

#### Scenario: Missing database
- **WHEN** the server starts with a `--db` path that does not exist
- **THEN** it prints its usage on stderr and exits with code 1, without creating a database

### Requirement: Search files and folders
The server SHALL expose `search_files` and `search_dirs`, matching words as prefixes against names and paths, ignoring case, accents and punctuation, optionally limited to a folder, best matches first. `search_dirs` SHALL report the number of files below each folder.

#### Scenario: Equivalent spellings
- **WHEN** an agent searches `DnD`
- **THEN** entries named `D&D …`, `DnD …` and `D and D …` are all returned

#### Scenario: Path typed with another case or slashes
- **WHEN** `pathPrefix` is `r:/jdr` and the collection root is `R:\JDR`
- **THEN** results are limited to `R:\JDR` and below

### Requirement: List a folder
The server SHALL expose `list_dir`, listing sub-folders (with file counts) then files, with an optional depth for sub-folders and a line limit.

#### Scenario: Folder outside every collection
- **WHEN** the path is not inside an indexed collection
- **THEN** the tool answers with the list of indexed folders instead of failing

### Requirement: Rescan a folder
The server SHALL expose `rescan(path)`, an incremental scan of that folder only, running one at a time in the background, and `index_status` to follow it.

#### Scenario: Files added in one folder
- **WHEN** files are added under `R:\JDR\OSR` and `rescan` is called on that folder
- **THEN** they become searchable, and entries outside `R:\JDR\OSR` are neither checked nor removed

### Requirement: Move, rename and delete through the index
The server SHALL expose `move(paths, destinationFolder, ifExists)`, `rename(path, newName, ifExists)` and `delete(paths)`, applying the change on disk and in the index, reporting one result per item, and sending deleted items to the recycle bin.

#### Scenario: Moving into another collection
- **WHEN** a file of collection "Non trié" is moved into a folder of collection "JDR"
- **THEN** its index row belongs to "JDR" and no rescan is needed to find it there

#### Scenario: One item fails
- **WHEN** one of the paths to move does not exist
- **THEN** that item reports an error and the other items are still moved

### Requirement: Operations journal
Every move, rename, copy and delete made through `FileOperationsService` (MCP server, Web, MAUI) SHALL be stored in the index database (`operations` table), one row per item with date, operation, outcome, path, destination and the program that did it, so operations can be counted.

#### Scenario: Counting operations
- **WHEN** an agent calls `index_status`
- **THEN** it gets the number of succeeded and failed operations per kind, over the last 7 days and all time

#### Scenario: Journal failure
- **WHEN** the journal cannot be written
- **THEN** the file operation still reports its own outcome, and the journal error is only logged
