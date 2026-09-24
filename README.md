# ServerSpace

Portable Windows Storage Analyzer

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
- Numeric sorting by size, files, folders, percentage and name
- Largest folders shown first by default
- Expand and collapse directory structures
- Virtualized result view for large directory trees
- Name filtering
- Minimum-size filtering
- Hide empty folders
- Configurable result columns
- Open directories directly in Windows Explorer
- Cancel running scans
- CSV report export
- TXT report export
- Partial report export after cancelled scans
- Portable single-file Windows executable
- Self-contained .NET runtime
- German and English user interface
- Runtime language switching
- Persistent language and window settings

---

## Languages

ServerSpace currently supports:

- Deutsch (de-DE)
- English (en-US)

The language can be changed directly from the application menu.

German:

Menü -> Sprache -> Deutsch / English

English:

Menu -> Language -> Deutsch / English

The selected language is stored locally and restored automatically on the next application start.

On the first start, ServerSpace uses the Windows user interface language where supported.

---

## Download

Prebuilt releases are available here:

https://github.com/Fletcher2k2k/ServerSpace/releases

Download ServerSpace.exe from the desired release.

No installer is required.

---

## System requirements

- Windows 10 x64
- Windows 11 x64
- Windows Server x64

The published portable build is self-contained.

A separate .NET installation is not required.

---

## Usage

1. Start ServerSpace.exe
2. Enter or select a directory
3. Click Scannen / Scan
4. Browse the results while the scan is running
5. Expand directories as required
6. Sort results by clicking a column header
7. Apply filters if required
8. Save a CSV or TXT report through the application menu

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

ServerSpace continues scanning accessible locations and records encountered errors.

---

## Network paths

ServerSpace supports:

- Local drives
- Local directories
- Mapped network drives
- UNC paths

Examples:

Z:\

\\SERVER\Share

\\SERVER\Share\Department

Network access uses the permissions and credentials of the Windows user running ServerSpace.

ServerSpace does not provide a separate credential-management system.

---

## Reports

ServerSpace can export the complete logical scan result as CSV or TXT.

Active UI filters do not restrict the exported scan tree.

### CSV

The CSV export is intended for further processing with tools such as Microsoft Excel.

The export contains information such as:

- Path
- Parent path
- Name
- Hierarchy level
- Formatted size
- Raw SizeBytes
- File count
- Directory count
- Percentage
- Status
- Last modification information

CSV characteristics:

- UTF-8
- Semicolon-separated
- Written line by line
- Complete logical scan tree
- Independent from active UI filters

Column names follow the currently selected ServerSpace language.

### TXT

TXT reports provide a human-readable hierarchical overview containing:

- Scan path
- Start time
- End time
- Duration
- Scan status
- Total size
- File count
- Directory count
- Error count
- Directory hierarchy
- Scan errors

Cancelled scans can also be exported as partial reports.

Report labels follow the currently selected application language.

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

This avoids creating thousands of WPF controls for directories that are not currently displayed.

---

## Architecture

ServerSpace is divided into separate components:

ServerSpace.Core

ServerSpace.Scanner

ServerSpace.UI

### ServerSpace.Core

Contains common models and interfaces used by the application.

### ServerSpace.Scanner

Contains the Windows file-system scanner.

The scanner is separated from the graphical interface so that additional storage providers can be added later without replacing the existing UI architecture.

### ServerSpace.UI

Contains:

- WPF user interface
- Localization
- Filtering
- Sorting
- Report export
- Window and language settings
- Application branding

---

## Technology

ServerSpace currently uses:

- C#
- .NET 10
- WPF
- Windows x64

---

## Application settings

ServerSpace stores a small local settings file under:

%LOCALAPPDATA%\ServerSpace\settings.json

The settings currently include:

- Selected language
- Window position
- Window size
- Window state

If the settings file is missing or invalid, ServerSpace falls back to safe defaults.

Stored window coordinates are validated so the application does not reopen completely outside the visible desktop area after monitor or resolution changes.

---

## Privacy

ServerSpace does not include telemetry, analytics or tracking.

ServerSpace does not automatically upload scan results, file metadata or file contents to external services.

File contents are not read during storage analysis.

Network communication only occurs when the user explicitly selects or enters a network path, mapped network drive or UNC share.

CSV and TXT reports are stored locally at a location selected by the user.

Application settings are stored locally on the user's computer.

ServerSpace will not transfer information to other networked systems unless specifically requested by the user through access to a user-selected network path or share.

---

## Security

ServerSpace runs with the permissions of the Windows user who starts the application.

Access to files and directories is limited to the permissions available to that user.

ServerSpace does not attempt to bypass Windows file-system permissions.

Network shares are accessed using the user's existing Windows credentials.

ServerSpace does not require administrative privileges for normal operation.

---

## Code signing policy

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

Project roles:

- Authors: Jürgen Schön
- Reviewers: Jürgen Schön
- Approvers: Jürgen Schön

The first public release, ServerSpace 0.1.0, was published before code signing was introduced.

Future releases are intended to use a reproducible and verifiable build and signing process.

---

## Open Source

ServerSpace is free and open source software.

The source code is released under the MIT License.

See:

LICENSE

The MIT License allows the source code to be used, modified, distributed and included in commercial projects subject to the conditions of the license.

---

## Trademark and branding

Copyright © 2026 Jürgen Schön.

The ServerSpace source code is licensed under the MIT License.

The ServerSpace name, logo and other official project branding are not granted for use as the branding of modified or derivative distributions.

The ServerSpace name may be used for attribution, compatibility information and references to the original project.

Forks and modified versions may use, modify and distribute the source code under the MIT License, but should use their own name and branding to avoid confusion with the official ServerSpace project.

This branding policy does not restrict the rights granted to the source code under the MIT License.

---

## Development

Developed by Jürgen Schön.

ServerSpace was developed with the assistance of artificial intelligence.

Concept, architecture, feature selection, implementation decisions, testing and release approval remain under the responsibility of the developer.

---

## Planned development

Possible future additions include:

- Large-file analysis
- File-type analysis
- Additional languages
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

## Current public release

ServerSpace 0.1.0

The first public release provides the initial Windows storage-analysis functionality.

Development on the main branch may contain features and improvements that are not yet included in the latest public release.

---

## Repository

GitHub:

https://github.com/Fletcher2k2k/ServerSpace

The repository contains:

- Source code
- Development history
- Issue tracking
- Release information
- License information

---

## Releases

Official releases are published here:

https://github.com/Fletcher2k2k/ServerSpace/releases

For release builds, verify the published SHA256 checksum where provided.

---

## License

MIT License

Copyright © 2026 Jürgen Schön