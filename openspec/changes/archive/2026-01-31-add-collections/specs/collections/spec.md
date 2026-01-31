## ADDED Requirements

### Requirement: Create collection
The system SHALL allow users to create a new collection with a name and optional description.

#### Scenario: Create collection with valid name
- **WHEN** user enters a collection name and clicks "Create"
- **THEN** the collection is created and appears in the collections list

#### Scenario: Create collection with duplicate name
- **WHEN** user enters a name that already exists
- **THEN** the system displays an error and does not create the collection

### Requirement: Edit collection
The system SHALL allow users to edit an existing collection's name and description.

#### Scenario: Edit collection name
- **WHEN** user changes the collection name and saves
- **THEN** the collection is updated with the new name

### Requirement: Delete collection
The system SHALL allow users to delete a collection.

#### Scenario: Delete collection with indexed files
- **WHEN** user deletes a collection that has indexed files
- **THEN** the collection and all its indexed files are removed from the database

#### Scenario: Delete empty collection
- **WHEN** user deletes a collection with no indexed files
- **THEN** the collection is removed from the database

### Requirement: Manage collection paths
The system SHALL allow users to add and remove root paths for each collection.

#### Scenario: Add path to collection
- **WHEN** user adds a valid directory path to a collection
- **THEN** the path is saved and displayed in the collection's path list

#### Scenario: Remove path from collection
- **WHEN** user removes a path from a collection
- **THEN** the path is removed but previously indexed files from that path remain

#### Scenario: Add invalid path
- **WHEN** user adds a path that does not exist on the filesystem
- **THEN** the system displays an error and does not add the path

### Requirement: Path overlap warning
The system SHALL warn users when adding a path that overlaps with another collection's path.

#### Scenario: Add overlapping path
- **WHEN** user adds a path that is a parent or child of a path in another collection
- **THEN** the system displays a warning showing which collection(s) overlap
- **AND** the system allows the user to proceed anyway

#### Scenario: Add exact duplicate path
- **WHEN** user adds a path that exactly matches a path in another collection
- **THEN** the system displays a warning showing which collection has this path
- **AND** the system allows the user to proceed anyway

### Requirement: Index collection
The system SHALL allow users to trigger indexing for a specific collection.

#### Scenario: Index collection with paths
- **WHEN** user clicks "Index" on a collection with configured paths
- **THEN** the scanner indexes all files under the collection's paths
- **AND** indexed files are associated with that collection

#### Scenario: Index collection with no paths
- **WHEN** user clicks "Index" on a collection with no paths configured
- **THEN** the system displays an error indicating no paths to index

#### Scenario: Re-index collection
- **WHEN** user indexes a collection that was previously indexed
- **THEN** the system clears existing files for that collection and re-indexes from scratch

### Requirement: Display collection statistics
The system SHALL display statistics for each collection on the Collections page.

#### Scenario: Collection with indexed files
- **WHEN** viewing a collection that has been indexed
- **THEN** the system displays file count and last indexed timestamp

#### Scenario: Collection never indexed
- **WHEN** viewing a collection that has never been indexed
- **THEN** the system displays "Never indexed" status

### Requirement: Filter search by collections
The system SHALL allow users to filter search results by one or more collections.

#### Scenario: Filter by single collection
- **WHEN** user selects one collection in the filter
- **THEN** search results show only files indexed in that collection

#### Scenario: Filter by multiple collections
- **WHEN** user selects multiple collections in the filter
- **THEN** search results show files from all selected collections
- **AND** results are deduplicated by file path

#### Scenario: No collection filter (all)
- **WHEN** user has no collections selected or selects "All"
- **THEN** search results show files from all collections
- **AND** results are deduplicated by file path

### Requirement: Collections page navigation
The system SHALL provide navigation to the Collections page.

#### Scenario: Access collections page
- **WHEN** user clicks "Collections" in the navigation menu
- **THEN** the Collections page is displayed showing all collections
