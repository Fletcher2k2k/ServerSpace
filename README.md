# ServerSpace

**Portable Windows Storage Analyzer**

ServerSpace is a lightweight and portable storage analyzer for Windows clients and servers.

It scans local folders, mapped network drives and UNC shares and displays disk usage in a fast hierarchical view.

ServerSpace is free and open source.

---

## Features

- Local file system scanning
- Mapped network drive support
- UNC network path support
- Live scan progress
- Hierarchical folder analysis
- Folder size visualization
- Numeric sorting by:
  - Size
  - Files
  - Folders
  - Percentage
  - Name
- Largest folders shown first by default
- Expand and collapse directory structures
- Virtualized result view for large directory trees
- Search and filtering:
  - Name contains
  - Minimum size
  - Hide empty folders
- Configurable result columns
- Open directories directly in Windows Explorer
- Cancel running scans
- CSV report export
- TXT report export
- Partial report export after cancelled scans
- Portable single-file Windows executable
- Self-contained .NET runtime

---

## Download

Prebuilt releases are available on the GitHub Releases page:

https://github.com/Fletcher2k2k/ServerSpace/releases

Download `ServerSpace.exe` from the latest release.

No installer is required.

---

## System requirements

- Windows 10 x64
- Windows 11 x64
- Windows Server x64

The portable build is self-contained.

A separate .NET installation is not required.

---

## Usage

1. Start `ServerSpace.exe`
2. Enter or select a directory
3. Click **Scannen**
4. Browse the results while the scan is running
5. Expand directories as required
6. Sort results by clicking a column header
7. Save a report through the application menu

Example paths:

C:\Windows
D:\Data
Z:\
\\FILESERVER\Data
\\FILESERVER\Data\Customers

Network paths are accessed using the credentials of the Windows user running ServerSpace.

---

## Storage analysis

ServerSpace analyzes file-system metadata to determine directory sizes.

File contents are not opened or analyzed.

During a scan, ServerSpace collects information such as:

- File size
- Directory structure
- File count
- Directory count
- Last modification information

Reparse points, junctions and symbolic links are not recursively followed.

File-system errors such as inaccessible directories do not terminate the complete scan.

---

## Reports

ServerSpace can export scan results as CSV or TXT.

### CSV

Suitable for further processing with tools such as Microsoft Excel.

The CSV export includes:

- Path
- Parent path
- Name
- Hierarchy level
- Formatted size
- Raw `SizeBytes`
- File count
- Directory count
- Percentage
- Status
- Last modification information

### TXT

Creates a human-readable hierarchical report containing:

- Scan information
- Scan duration
- Total size
- File count
- Directory count
- Error count
- Directory hierarchy
- Scan errors

Cancelled scans can also be exported as partial reports.

Active UI filters do not restrict the exported logical scan result.

---

## Performance

ServerSpace is designed to remain responsive while scanning large directory structures.

The user interface uses:

- Virtualized WPF DataGrid
- Row recycling
- Lazy directory expansion
- Flattened visible-row representation
- Throttled UI updates
- Latest-state-wins directory updates

Only directories currently required for display are represented as visible UI rows.

The complete logical scan result remains available independently from the visible interface.

---

## Architecture

ServerSpace is divided into separate components:

ServerSpace.Core
ServerSpace.Scanner
ServerSpace.UI

### ServerSpace.Core

Contains the common models and interfaces used by the application.

### ServerSpace.Scanner

Contains the Windows file-system scanner.

The scanner is designed to work independently from the graphical interface.

### ServerSpace.UI

Contains the WPF user interface and report functionality.

This separation allows additional storage providers and monitoring functions to be added in the future without replacing the complete application architecture.

---

## Technology

ServerSpace currently uses:

- C#
- .NET 10
- WPF
- Windows x64

---

## Privacy

ServerSpace does not include telemetry, analytics or tracking.

ServerSpace does not automatically upload scan results, file metadata or file contents to external services.

File contents are not read during storage analysis.

Network communication only occurs when the user explicitly selects or enters a network path, mapped network drive or UNC share.

CSV and TXT reports are stored locally at a location selected by the user.

ServerSpace will not transfer information to other networked systems unless specifically requested by the user through access to a user-selected network path or share.

---

## Code signing policy

Free code signing provided by **SignPath.io**, certificate by **SignPath Foundation**.

Project roles:

- Authors: Jürgen Schön
- Reviewers: Jürgen Schön
- Approvers: Jürgen Schön

The first public release, ServerSpace `0.1.0`, was published before code signing was introduced.

Future releases are intended to use a reproducible and verifiable build and signing process.

---

## Security

ServerSpace runs with the permissions of the Windows user who starts the application.

Access to files and directories is limited to the permissions available to that user.

ServerSpace does not attempt to bypass Windows file-system permissions.

Network shares are accessed using the user's existing Windows credentials.

---

## Planned development

Possible future additions include:

- Large-file analysis
- File-type analysis
- Storage history
- Capacity trends
- HTML reports
- NAS integration
- SAN / storage integration
- Proxmox integration
- VMware ESXi / vCenter integration
- Hyper-V integration
- Storage monitoring
- Infrastructure monitoring

The roadmap is intentionally open and may change as the project develops.

---

## Open Source

ServerSpace is free and open source software.

It is released under the **MIT License**.

See:

LICENSE

---

## Development

Developed by **Jürgen Schön**.

ServerSpace was developed with the assistance of artificial intelligence.

Architecture, feature selection, implementation decisions, testing and release approval remain under the responsibility of the developer.

---

## Current version

**ServerSpace 0.1.0**

The first public release provides the initial Windows storage-analysis functionality.

---

## Repository

GitHub:

https://github.com/Fletcher2k2k/ServerSpace

Issues, source code and releases are maintained through this repository.

---

## License

MIT License

Copyright © 2026 Jürgen Schön