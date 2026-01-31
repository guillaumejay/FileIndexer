## ADDED Requirements

### Requirement: Right-side path truncation
When a directory path exceeds the display width, the system SHALL truncate from the right side, showing the beginning of the path followed by ellipsis.

#### Scenario: Long path truncation
- **WHEN** a directory path exceeds the maximum display length
- **THEN** the system SHALL display the beginning of the path
- **AND** append "..." at the end to indicate truncation

#### Scenario: Short path display
- **WHEN** a directory path fits within the maximum display length
- **THEN** the system SHALL display the complete path without truncation

### Requirement: Full path tooltip
The directory column SHALL display the complete path in a tooltip when the user hovers over it.

#### Scenario: Tooltip on truncated path
- **WHEN** a user hovers over a truncated directory path
- **THEN** a tooltip SHALL appear showing the complete directory path

#### Scenario: Tooltip on non-truncated path
- **WHEN** a user hovers over a non-truncated directory path
- **THEN** a tooltip SHALL appear showing the complete directory path
