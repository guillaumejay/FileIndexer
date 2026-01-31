## ADDED Requirements

### Requirement: Compact row height
The file list table SHALL display rows with a height of 22 pixels maximum, using minimal padding (2px vertical, 8px horizontal).

#### Scenario: Row density increased
- **WHEN** the file list is displayed
- **THEN** each row height SHALL be 22px or less
- **AND** approximately 64% more rows SHALL be visible compared to the previous 36px rows

### Requirement: Visible grid borders
The file list table SHALL display visible borders between all cells, creating a DataGrid appearance.

#### Scenario: Column separators visible
- **WHEN** the file list is displayed
- **THEN** vertical borders SHALL be visible between each column
- **AND** horizontal borders SHALL be visible between each row

### Requirement: Zebra striping
The file list table SHALL alternate row background colors to improve horizontal tracking.

#### Scenario: Alternating row colors
- **WHEN** the file list is displayed
- **THEN** even-numbered rows SHALL have a slightly different background color than odd-numbered rows
- **AND** the color difference SHALL be subtle but visible in both light and dark themes

### Requirement: Monospace font for data
The file list table cells SHALL use a monospace or condensed font for data consistency.

#### Scenario: Consistent character width
- **WHEN** file information is displayed
- **THEN** the file name, directory, extension, size, and date columns SHALL use a monospace font
- **AND** columns SHALL align vertically across rows

### Requirement: Compact header style
The table headers SHALL match the compact DataGrid style with minimal padding.

#### Scenario: Header consistency
- **WHEN** the file list is displayed
- **THEN** header cells SHALL have the same border style as data cells
- **AND** header padding SHALL be consistent with data cell padding
