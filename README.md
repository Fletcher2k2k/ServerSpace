# ServerSpace

**Portable Windows Storage Analyzer for Windows**

ServerSpace is a lightweight, portable storage analyzer for Windows clients and servers.  
It scans local paths, mapped network drives and UNC shares, then presents folder sizes in a fast, hierarchical view.

The project is free and open source.

## Features

- Local file system scans
- UNC paths such as `\\SERVER\Share`
- Mapped network drives
- Live scan progress
- Hierarchical folder view
- Folder size bars
- Numeric sorting by:
  - size
  - files
  - folders
  - percentage
  - name
- Largest folders shown first by default
- Expand / collapse without materializing the complete tree in the UI
- Virtualized WPF DataGrid for large directory structures
- Filters:
  - name contains
  - minimum size
  - hide empty folders
- Optional columns
- Open scan paths directly in Windows Explorer
- Cancel running scans
- CSV report export
- TXT report export
- Partial reports for cancelled scans
- Portable single-file Windows executable
- Self-contained .NET runtime

## Download

Prebuilt versions are available from the GitHub Releases page:

**https://github.com/Fletcher2k2k/ServerSpace/releases**

For the latest stable version, open the newest release and download `ServerSpace.exe`.

## System requirements

- Windows 10 x64
- Windows 11 x64
- Windows Server x64

The published portable build is self-contained.  
A separate .NET installation is not required.

## Usage

1. Start `ServerSpace.exe`.
2. Enter or select a folder.
3. Click **Scannen**.
4. Browse the results while the scan is running.
5. Sort the result by clicking a column header.
6. Open folders with a double-click or via the context menu.
7. Save a report through the application menu.

Examples:

```text
C:\Windows
D:\Data
Z:\
\\FILESERVER\Data
\\FILESERVER\Data\Customers
```

Network access uses the credentials of the Windows user running ServerSpace.

## Reports

ServerSpace can export the complete logical scan result as:

- **CSV** — suitable for further processing in Excel or other tools
- **TXT** — human-readable hierarchical report

CSV exports include the raw numeric `SizeBytes` value so size data can be sorted and processed reliably.

Active UI filters do not limit the exported report.

## Architecture

ServerSpace is split into separate components:

```text
ServerSpace.Core
ServerSpace.Scanner
ServerSpace.UI
```

The scanner is separated from the UI so additional providers and monitoring functions can be added later without rebuilding the complete application architecture.

The UI uses:

- a virtualized `DataGrid`
- row recycling
- lazy expand / collapse
- a flattened visible-row model
- latest-state-wins live updates

This keeps the interface responsive even when scanning large directory trees.

## Planned development

Possible future additions include:

- NAS integration
- SAN / storage integration
- Proxmox integration
- VMware ESXi / vCenter integration
- Hyper-V integration
- storage history
- capacity trends
- monitoring
- HTML reports
- large-file analysis

The roadmap is intentionally open and may change as the project develops.

## Open source

ServerSpace is released under the **MIT License**.

See [LICENSE](LICENSE).

## Development

Developed by **Jürgen Schön**.

ServerSpace was developed with the assistance of artificial intelligence.

Architecture, feature selection, testing and release decisions remain under the responsibility of the developer.

## Version

Current first public version:

**ServerSpace 0.1.0**

## Repository

https://github.com/Fletcher2k2k/ServerSpace
