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
- Column sorting by name, size, files, folders and percentage
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
- ServerSpace application branding and icon
- Integrated Info / About window with project and license links

---

## What's new in 0.2.0

ServerSpace 0.2.0 focuses on localization, application polish and persistent user settings.

Highlights include:

- German and English user interface
- Runtime language switching
- Automatic initial language selection
- Persistent language setting
- Persistent window position and size
- Restored maximized window state
- Validation of stored window coordinates
- ServerSpace application icon and branding
- Updated Info / About window
- Direct project, organization and MIT License links
- Improved application menu
- Configurable result columns
- Additional interface and usability improvements
- Updated project documentation
- Updated EDVFUX repository links

The underlying scanning architecture remains focused on predictable, resource-conscious operation and responsive handling of large directory structures.

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

If the Windows user interface language is not German, ServerSpace uses English by default.

---

## Download

Official releases are available here:

https://github.com/edvfux/ServerSpace/releases

Download ServerSpace.exe from the desired release.

No installer is required.

ServerSpace is distributed as a portable Windows executable.

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

Network paths are accessed using the credentials and permissions of the Windows user running ServerSpace.

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

This prevents directory loops and unintended traversal into linked directory structures.

File-system errors such as inaccessible or disappearing directories do not terminate the complete scan.

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

ServerSpace does not provide or store a separate network credential-management system.

If the current Windows user cannot access a network location, ServerSpace does not attempt to bypass those permissions.

---

## Scan behavior

The scanner is designed to operate in a controlled and resource-conscious manner.

ServerSpace:

- Uses the current Windows security context
- Processes file-system metadata only
- Supports cancellation
- Continues after recoverable file-system errors
- Records scan errors for later review
- Avoids recursive traversal of reparse points
- Updates visible scan information while scanning
- Maintains the logical directory tree independently from the visible user interface

A cancelled scan can still be used to create a partial report from the information collected up to that point.

---

## Result view

ServerSpace uses a virtualized flat result view instead of creating a graphical control for every scanned directory.

Directories are displayed hierarchically using indentation.

Expanding a directory adds its direct children to the visible result list.

Collapsing a directory removes its visible descendants without deleting the underlying scan data.

This allows ServerSpace to work with large directory structures while keeping the graphical interface responsive.

---

## Sorting

The result view can be sorted by:

- Name
- Size
- Files
- Folders
- Percentage

Directory hierarchy is preserved while sorting.

The default view shows larger directories first.

---

## Filters

ServerSpace currently provides:

- Name filtering
- Minimum-size filtering
- Hide empty folders

Minimum-size filtering supports size units such as:

- MB
- GB
- TB

Filtering affects the visible result view.

The complete logical scan tree remains available independently from active UI filters.

---

## Columns

Result columns can be enabled or disabled through the View menu.

Available information includes:

- Name
- Graphical size bar
- Size
- Files
- Folders
- Percentage

The Name column remains available as the primary directory representation.

---

## Windows Explorer integration

Directories can be opened directly in Windows Explorer.

Explorer integration is available through the application interface for scanned directories.

---

## Reports

ServerSpace can export the complete logical scan result as CSV or TXT.

Active UI filters do not restrict the exported scan tree.

Cancelled scans can also be exported as partial reports.

Report labels follow the currently selected ServerSpace language.

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

Cancelled scans can be exported as partial TXT reports.

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

- ServerSpace.Core
- ServerSpace.Scanner
- ServerSpace.UI

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
- Info / About interface

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
- Maximized window state

A minimized window state is not restored on application startup.

If the settings file is missing or invalid, ServerSpace falls back to safe defaults.

Stored window coordinates are validated so the application does not reopen completely outside the visible desktop area after monitor or resolution changes.

---

## Privacy

ServerSpace does not include telemetry, analytics or tracking.

ServerSpace does not automatically upload scan results, file metadata, reports or file contents to external services.

File contents are not read during storage analysis.

ServerSpace analyzes file-system metadata only.

When the user scans a mapped network drive or UNC path, ServerSpace accesses only the network location selected by the user and uses the permissions and credentials of the current Windows user.

ServerSpace does not store separate network credentials.

CSV and TXT reports are stored locally at a location selected by the user.

Reports may contain information such as:

- Directory paths
- Directory names
- File and directory counts
- Directory sizes
- Timestamps
- Scan-error information

Depending on the scanned environment, report information may be sensitive and should be handled accordingly.

Application settings are stored locally under:

%LOCALAPPDATA%\ServerSpace\settings.json

ServerSpace does not make background Internet connections for telemetry, analytics or tracking.

Links opened from ServerSpace, such as project, organization or license links, are opened in the user's default web browser.

Any resulting Internet connection is handled by that browser.

---

## Security

ServerSpace runs with the permissions of the Windows user who starts the application.

Access to files and directories is limited to the permissions available to that user.

ServerSpace does not attempt to bypass Windows file-system permissions.

Network shares are accessed using the user's existing Windows credentials.

ServerSpace does not store separate network-share credentials.

ServerSpace does not require administrative privileges for normal operation.

Reparse points, junctions and symbolic links are not recursively followed during normal scanning.

---

## Code signing

ServerSpace releases are currently distributed unsigned.

Because ServerSpace is a new open source project, Windows SmartScreen may display a warning when running downloaded releases.

Release binaries include a published SHA256 checksum where available so users can verify file integrity.

Digitally signed Windows releases are planned for a future version.

---

## License

ServerSpace is free and open source software released under the MIT License.

Copyright © 2026 Jürgen Schön.

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

- Digitally signed Windows releases
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

## Current release

ServerSpace 0.2.0

ServerSpace 0.2.0 expands the original storage-analysis functionality with localization, persistent settings, ServerSpace branding and additional user-interface improvements.

The application remains a portable, self-contained Windows x64 executable and does not require a separate .NET installation.

---

## Repository

Official GitHub repository:

https://github.com/edvfux/ServerSpace

The repository contains:

- Source code
- Development history
- Issue tracking
- Release information
- License information

---

## Releases

Official releases are published here:

https://github.com/edvfux/ServerSpace/releases

Release builds may include a published SHA256 checksum for integrity verification.

ServerSpace 0.2.0 is distributed as a portable Windows x64 executable.