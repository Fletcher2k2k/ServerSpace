using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using ServerSpace.Core.Models;
using ServerSpace.Scanner;
using ServerSpace.UI.Localization;
using ServerSpace.UI.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using WinSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace ServerSpace.UI;

public partial class MainWindow : Window
{
    private const int MaxDirectoryUpdatesPerTick = 250;

    private readonly WindowsFileSystemScanner _scanner = new();
    private readonly ReportExporter _reportExporter = new();
    private readonly Stopwatch _scanStopwatch = new();
    private readonly DispatcherTimer _runtimeTimer;
    private readonly DispatcherTimer _progressUpdateTimer;
    private readonly DispatcherTimer _treeUpdateTimer;
    private readonly DispatcherTimer _liveSortTimer;
    private readonly DispatcherTimer _filterTimer;
    private readonly VisibleRowsCollection _visibleRows = new();
    private readonly Dictionary<string, LiveDirectory> _logicalNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<LiveDirectory> _expandedNodes = [];
    private readonly Dictionary<string, DirectoryScanUpdate> _pendingLatestUpdates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _pendingPaths = new();
    private readonly HashSet<string> _scheduledPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pendingUpdatesLock = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private ScanProgress? _latestProgress;
    private LiveDirectory? _rootNode;
    private ScanResult? _lastScanResult;
    private string? _reportFilePath;
    private bool _scanRunning;
    private bool _scanFinished;
    private SortColumn _sortColumn = SortColumn.Size;
    private bool _sortDescending = true;
    private bool _hasUserSortSelection;
    private bool _automaticLiveSizeSort;
    private int _sortVersion = 1;
    private bool _filterActive;
    private bool _filterDirty;
    private bool _uiInitialized;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _runtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _runtimeTimer.Tick += RuntimeTimer_Tick;

        _progressUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
        _progressUpdateTimer.Tick += ProgressUpdateTimer_Tick;

        _treeUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(225) };
        _treeUpdateTimer.Tick += TreeUpdateTimer_Tick;

        _liveSortTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _liveSortTimer.Tick += LiveSortTimer_Tick;

        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _filterTimer.Tick += FilterTimer_Tick;
        LocalizationManager.RestoreWindowPlacement(this);
        _uiInitialized = true;
        UpdateLanguageChecks();
        UpdateLocalizedDataGridHeaders();
    }

    public VisibleRowsCollection VisibleRows => _visibleRows;

    private void OpenPathButton_Click(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(PathTextBox.Text.Trim());
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AboutWindow aboutWindow = new()
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string tag })
        {
            return;
        }

        LocalizationManager.SetLanguage(
            System.Windows.Application.Current,
            tag == "German" ? AppLanguage.German : AppLanguage.English);
        UpdateLanguageChecks();
        UpdateLocalizedDataGridHeaders();
    }

    private void UpdateLocalizedDataGridHeaders()
    {
        if (ResultsDataGrid.Columns.Count < 6)
        {
            return;
        }

        ResultsDataGrid.Columns[0].Header = LocalizationManager.GetString("ColumnName");
        ResultsDataGrid.Columns[1].Header = LocalizationManager.GetString("ColumnSizeBar");
        ResultsDataGrid.Columns[2].Header = LocalizationManager.GetString("ColumnSize");
        ResultsDataGrid.Columns[3].Header = LocalizationManager.GetString("ColumnFiles");
        ResultsDataGrid.Columns[4].Header = LocalizationManager.GetString("ColumnFolders");
        ResultsDataGrid.Columns[5].Header = LocalizationManager.GetString("ColumnPercentage");
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        LocalizationManager.SaveWindowPlacement(this);
    }

    private void UpdateLanguageChecks()
    {
        bool german = LocalizationManager.CurrentLanguage == AppLanguage.German;
        GermanLanguageMenuItem.IsChecked = german;
        EnglishLanguageMenuItem.IsChecked = !german;
    }

    private void ResultsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        int columnIndex = ResultsDataGrid.Columns.IndexOf(e.Column);
        SortColumn? column = columnIndex switch
        {
            0 => SortColumn.Name,
            2 => SortColumn.Size,
            3 => SortColumn.Files,
            4 => SortColumn.Directories,
            5 => SortColumn.Share,
            _ => null
        };
        e.Handled = true;
        if (column is null)
        {
            return;
        }

        if (_hasUserSortSelection && _sortColumn == column.Value)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _sortColumn = column.Value;
            _sortDescending = column.Value != SortColumn.Name;
            _hasUserSortSelection = true;
        }

        _automaticLiveSizeSort = false;
        _sortVersion++;
        RebuildVisibleRows();
    }

    private void FilterMenuItem_Checked(object sender, RoutedEventArgs e)
    {
        SetFilterPanelVisibility(isVisible: true);
    }

    private void FilterMenuItem_Unchecked(object sender, RoutedEventArgs e)
    {
        SetFilterPanelVisibility(isVisible: false);
    }

    private void SetFilterPanelVisibility(bool isVisible)
    {
        FilterPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        UpdateFilterInputState();
    }

    private void FilterOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_uiInitialized)
        {
            return;
        }

        UpdateFilterInputState();
        _filterDirty = true;
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void UpdateFilterInputState()
    {
        if (!_uiInitialized)
        {
            return;
        }

        NameFilterTextBox.IsEnabled = NameFilterCheckBox.IsChecked == true;
        MinimumSizeTextBox.IsEnabled = MinimumSizeCheckBox.IsChecked == true;
        MinimumSizeUnitComboBox.IsEnabled = MinimumSizeCheckBox.IsChecked == true;
    }

    private void ColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string tag } item
            || !int.TryParse(tag, out int columnIndex))
        {
            return;
        }
        if (columnIndex >= 0)
        {
            ResultsDataGrid.Columns[columnIndex].Visibility = item.IsChecked
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void FilterTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_uiInitialized || _filterTimer is null)
        {
            return;
        }

        _filterDirty = true;
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void FilterUnitChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_uiInitialized || _filterTimer is null)
        {
            return;
        }

        _filterDirty = true;
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void FilterTimer_Tick(object? sender, EventArgs e)
    {
        _filterTimer.Stop();
        if (_filterDirty)
        {
            _filterDirty = false;
            RebuildVisibleRows();
        }
    }

    private void ResetFilterButton_Click(object sender, RoutedEventArgs e)
    {
        NameFilterCheckBox.IsChecked = false;
        MinimumSizeCheckBox.IsChecked = false;
        HideEmptyFoldersCheckBox.IsChecked = false;
        NameFilterTextBox.Clear();
        MinimumSizeTextBox.Clear();
        MinimumSizeUnitComboBox.SelectedIndex = 0;
        _filterDirty = false;
        _filterTimer.Stop();
        RebuildVisibleRows();
    }

    private void OpenRowInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem
            && menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu
            && contextMenu.PlacementTarget is DataGridRow row
            && row.DataContext is DirectoryRowViewModel directoryRow)
        {
            OpenInExplorer(directoryRow.FullPath);
        }
    }

    private void ResultsDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        System.Windows.Controls.ContextMenu contextMenu = new();
        System.Windows.Controls.MenuItem menuItem = new()
        {
            Header = LocalizationManager.GetString("ContextOpenExplorer")
        };
        menuItem.Click += OpenRowInExplorer_Click;
        contextMenu.Items.Add(menuItem);
        e.Row.ContextMenu = contextMenu;
    }

    private void ResultsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(ResultsDataGrid, source) is DataGridRow row
            && row.DataContext is DirectoryRowViewModel directoryRow)
        {
            OpenInExplorer(directoryRow.FullPath);
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        using Forms.FolderBrowserDialog dialog = new()
        {
            Description = LocalizationManager.GetString("BrowseDescription"),
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(PathTextBox.Text) ? PathTextBox.Text : string.Empty
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            PathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveReportMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SaveReport(saveAs: false);
    }

    private void SaveReportAsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SaveReport(saveAs: true);
    }

    private void SaveReport(bool saveAs)
    {
        if (_lastScanResult is null)
        {
            return;
        }

        if (!saveAs && !string.IsNullOrWhiteSpace(_reportFilePath))
        {
            ExportReport(_reportFilePath);
            return;
        }

        WinSaveFileDialog dialog = new()
        {
            Filter = LocalizationManager.GetString("DialogReportFilter"),
            FilterIndex = 1,
            AddExtension = true,
            DefaultExt = ".csv",
            OverwritePrompt = true,
            FileName = ReportExporter.CreateSuggestedFileName(
                _lastScanResult.Root.FullPath,
                _lastScanResult.StartedAt)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _reportFilePath = dialog.FileName;
            ExportReport(_reportFilePath);
        }
        catch (Exception exception)
        {
            ExceptionLogger.Log(exception);
            ShowError(LocalizationManager.Format("ErrorReportSave", exception.Message));
        }
    }

    private void ExportReport(string filePath)
    {
        try
        {
            _reportExporter.Export(_lastScanResult!, filePath);
            System.Windows.MessageBox.Show(
                this,
                LocalizationManager.GetString("ReportSaved"),
                LocalizationManager.GetString("DialogReportTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ExceptionLogger.Log(exception);
            ShowError(LocalizationManager.Format("ErrorReportSave", exception.Message));
        }
    }

    private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.S
            && System.Windows.Input.Keyboard.Modifiers == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
        {
            SaveReport(saveAs: true);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.S
                 && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            SaveReport(saveAs: false);
            e.Handled = true;
        }
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        string path = PathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowError(LocalizationManager.GetString("ErrorInvalidPath"));
            return;
        }

        SetScanningState(isScanning: true);
        ResetScanDisplay();
        _sortColumn = SortColumn.Size;
        _sortDescending = true;
        _hasUserSortSelection = false;
        _automaticLiveSizeSort = true;
        _sortVersion++;
        _scanStopwatch.Restart();
        _runtimeTimer.Start();
        _progressUpdateTimer.Start();
        _treeUpdateTimer.Start();
        _liveSortTimer.Start();
        _scanRunning = true;
        _scanFinished = false;
        _cancellationTokenSource = new CancellationTokenSource();

        QueueProgressSink progress = new(EnqueueProgress);

        try
        {
            InitializeLiveTree(path);
            ScanResult result = await _scanner.ScanAsync(
                path,
                progress,
                _cancellationTokenSource.Token);

            ApplyFinalResult(result);
            _scanFinished = true;
        }
        catch (Exception exception)
        {
            ExceptionLogger.Log(exception);
            ShowError(exception.Message);
            _scanFinished = true;
        }
        finally
        {
            _scanRunning = false;
            _runtimeTimer.Stop();
            _progressUpdateTimer.Stop();
            _liveSortTimer.Stop();
            _scanStopwatch.Stop();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            SetScanningState(isScanning: false);

            if (PendingUpdateCount == 0)
            {
                _treeUpdateTimer.Stop();
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        CancelButton.IsEnabled = false;
        ResultTextBlock.Text = LocalizationManager.GetString("StatusAbortPending");
    }

    private void ToggleRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: DirectoryRowViewModel row })
        {
            return;
        }

        if (!row.HasChildren)
        {
            return;
        }

        if (row.IsExpanded)
        {
            CollapseRow(row);
        }
        else
        {
            ExpandRow(row);
        }
    }

    private void RuntimeTimer_Tick(object? sender, EventArgs e)
    {
        DurationTextBlock.Text = FormatDuration(_scanStopwatch.Elapsed);
    }

    private void ProgressUpdateTimer_Tick(object? sender, EventArgs e)
    {
        ScanProgress? latestProgress = Volatile.Read(ref _latestProgress);
        if (latestProgress is not null)
        {
            UpdateProgress(latestProgress);
        }
    }

    private void TreeUpdateTimer_Tick(object? sender, EventArgs e)
    {
        List<DirectoryScanUpdate> updates = DequeueUpdates(MaxDirectoryUpdatesPerTick);
        if (updates.Count > 0)
        {
            foreach (DirectoryScanUpdate update in updates.OrderBy(GetPathDepth))
            {
                ApplyDirectoryUpdate(update);
            }

            UpdateVisibleRows();
            if (_filterActive)
            {
                RebuildVisibleRows();
            }
        }

        if (!_scanRunning && _scanFinished && PendingUpdateCount == 0)
        {
            _sortVersion++;
            RebuildVisibleRows();
            _treeUpdateTimer.Stop();
        }
    }

    private void LiveSortTimer_Tick(object? sender, EventArgs e)
    {
        if (_scanRunning && _automaticLiveSizeSort)
        {
            SortVisibleSiblingGroups(SortColumn.Size, descending: true);
        }
    }

    private void EnqueueProgress(ScanProgress progress)
    {
        Volatile.Write(ref _latestProgress, progress);

        lock (_pendingUpdatesLock)
        {
            foreach (DirectoryScanUpdate update in progress.DirectoryUpdates)
            {
                _pendingLatestUpdates[update.FullPath] = update;
                if (_scheduledPaths.Add(update.FullPath))
                {
                    _pendingPaths.Enqueue(update.FullPath);
                }
            }
        }
    }

    private List<DirectoryScanUpdate> DequeueUpdates(int maximum)
    {
        List<DirectoryScanUpdate> updates = new(maximum);

        lock (_pendingUpdatesLock)
        {
            while (updates.Count < maximum && _pendingPaths.Count > 0)
            {
                string path = _pendingPaths.Dequeue();
                _scheduledPaths.Remove(path);
                if (_pendingLatestUpdates.Remove(path, out DirectoryScanUpdate? update))
                {
                    updates.Add(update);
                }
            }
        }

        return updates;
    }

    private int PendingUpdateCount
    {
        get
        {
            lock (_pendingUpdatesLock)
            {
                return _pendingLatestUpdates.Count;
            }
        }
    }

    private void ApplyFinalResult(ScanResult result)
    {
        _lastScanResult = result;
        UpdateProgress(new ScanProgress
        {
            CurrentPath = result.Root.FullPath,
            FilesScanned = result.TotalFiles,
            DirectoriesScanned = result.TotalDirectories,
            BytesScanned = result.TotalBytes
        });
        DurationTextBlock.Text = FormatDuration(result.Duration);
        ErrorsTextBlock.Text = result.Errors.Count.ToString(CultureInfo.CurrentCulture);
        ResultTextBlock.Text = LocalizationManager.GetString(
            result.WasCancelled ? "StatusCancelled" : "StatusCompleted");

        ApplyDirectoryUpdate(new DirectoryScanUpdate
        {
            Name = result.Root.Name,
            FullPath = result.Root.FullPath,
            SizeBytes = result.Root.SizeBytes,
            FileCount = result.Root.FileCount,
            DirectoryCount = result.Root.DirectoryCount,
            LastWriteTime = result.Root.LastWriteTime,
            IsComplete = true
        });
        UpdateVisibleRows();
    }

    private void OpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            ShowError(LocalizationManager.Format("ErrorExplorerPath", path));
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            ExceptionLogger.Log(exception);
            ShowError(LocalizationManager.Format("ErrorExplorerOpen", exception.Message));
        }
    }

    private void RebuildVisibleRows()
    {
        if (_rootNode is null)
        {
            return;
        }

        bool nameFilterEnabled = NameFilterCheckBox.IsChecked == true;
        bool minimumSizeEnabled = MinimumSizeCheckBox.IsChecked == true;
        bool hideEmptyFolders = HideEmptyFoldersCheckBox.IsChecked == true;
        string nameFilter = nameFilterEnabled ? NameFilterTextBox.Text.Trim() : string.Empty;
        long? minimumSizeBytes = minimumSizeEnabled ? ParseMinimumSizeBytes() : null;
        _filterActive = !string.IsNullOrWhiteSpace(nameFilter)
            || minimumSizeBytes.HasValue
            || hideEmptyFolders;

        HashSet<LiveDirectory> included = _filterActive
            ? BuildFilterSet(nameFilter, minimumSizeBytes, hideEmptyFolders)
            : [];

        Dictionary<string, DirectoryRowViewModel> oldRows = new(_visibleRowsByPath, StringComparer.OrdinalIgnoreCase);
        foreach (DirectoryRowViewModel oldRow in oldRows.Values)
        {
            oldRow.Node.Row = null;
        }

        List<DirectoryRowViewModel> rows = [];
        _visibleRowsByPath.Clear();
        AppendVisibleRows(_rootNode, 0, included, rows, oldRows);

        _visibleRows.ReplaceAll(rows);
    }

    private void AppendVisibleRows(
        LiveDirectory node,
        int depth,
        HashSet<LiveDirectory> included,
        List<DirectoryRowViewModel> rows,
        Dictionary<string, DirectoryRowViewModel> oldRows)
    {
        if (_filterActive && !included.Contains(node))
        {
            return;
        }

        DirectoryRowViewModel row;
        if (oldRows.TryGetValue(node.FullPath, out DirectoryRowViewModel? oldRow))
        {
            row = oldRow;
            row.Depth = depth;
            row.UpdateFrom(node, _rootNode?.SizeBytes ?? 0);
        }
        else
        {
            row = new DirectoryRowViewModel(node, depth, _rootNode?.SizeBytes ?? 0);
        }

        node.Row = row;
        _visibleRowsByPath[node.FullPath] = row;
        rows.Add(row);

        bool showChildren = node.IsExpanded || _filterActive;
        if (!showChildren)
        {
            return;
        }

        EnsureChildrenSorted(node);
        foreach (LiveDirectory child in node.Children)
        {
            AppendVisibleRows(child, depth + 1, included, rows, oldRows);
        }
    }

    private HashSet<LiveDirectory> BuildFilterSet(
        string nameFilter,
        long? minimumSizeBytes,
        bool hideEmptyFolders)
    {
        HashSet<LiveDirectory> included = [];
        foreach (LiveDirectory node in _logicalNodes.Values)
        {
            bool nameMatches = string.IsNullOrWhiteSpace(nameFilter)
                || node.Name.Contains(nameFilter, StringComparison.CurrentCultureIgnoreCase);
            bool sizeMatches = !minimumSizeBytes.HasValue || node.SizeBytes >= minimumSizeBytes.Value;
            bool isEmpty = node.IsComplete && node.FileCount == 0 && node.DirectoryCount == 0;
            if (!nameMatches || !sizeMatches || (hideEmptyFolders && isEmpty))
            {
                continue;
            }

            LiveDirectory? current = node;
            while (current is not null)
            {
                if (!included.Add(current))
                {
                    break;
                }
                current = current.Parent;
            }
        }

        return included;
    }

    private long? ParseMinimumSizeBytes()
    {
        if (!double.TryParse(MinimumSizeTextBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
            || value <= 0
            || MinimumSizeUnitComboBox.SelectedItem is not ComboBoxItem unitItem
            || !long.TryParse((string)unitItem.Tag, CultureInfo.InvariantCulture, out long multiplier))
        {
            return null;
        }

        double bytes = value * multiplier;
        return bytes >= long.MaxValue ? long.MaxValue : (long)Math.Ceiling(bytes);
    }

    private void EnsureChildrenSorted(LiveDirectory node)
    {
        if (node.SortVersion == _sortVersion)
        {
            return;
        }

        node.Children.Sort(CompareDirectories);
        node.SortVersion = _sortVersion;
    }

    private int CompareDirectories(LiveDirectory left, LiveDirectory right)
    {
        return CompareDirectories(left, right, _sortColumn, _sortDescending);
    }

    private int CompareDirectories(
        LiveDirectory left,
        LiveDirectory right,
        SortColumn column,
        bool descending)
    {
        int result = column switch
        {
            SortColumn.Name => StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name),
            SortColumn.Files => left.FileCount.CompareTo(right.FileCount),
            SortColumn.Directories => left.DirectoryCount.CompareTo(right.DirectoryCount),
            SortColumn.Share => GetRootShare(left).CompareTo(GetRootShare(right)),
            _ => left.SizeBytes.CompareTo(right.SizeBytes)
        };

        if (descending)
        {
            result = -result;
        }

        return result != 0
            ? result
            : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
    }

    private double GetRootShare(LiveDirectory node)
    {
        return _rootNode?.SizeBytes > 0
            ? node.SizeBytes / (double)_rootNode.SizeBytes
            : 0;
    }

    private void InitializeLiveTree(string path)
    {
        _visibleRows.Clear();
        _logicalNodes.Clear();
        _expandedNodes.Clear();
        lock (_pendingUpdatesLock)
        {
            _pendingLatestUpdates.Clear();
            _pendingPaths.Clear();
            _scheduledPaths.Clear();
        }

        Volatile.Write(ref _latestProgress, null);

        string fullPath = Path.GetFullPath(path);
        string name = new DirectoryInfo(fullPath).Name;
        if (string.IsNullOrEmpty(name))
        {
            name = fullPath;
        }

        LiveDirectory root = new(fullPath, null, name)
        {
            IsExpanded = true
        };
        _rootNode = root;
        _logicalNodes.Add(root.FullPath, root);
        _expandedNodes.Add(root);

        DirectoryRowViewModel rootRow = CreateRow(root, 0);
        root.Row = rootRow;
        _visibleRows.Add(rootRow);
    }

    private void ApplyDirectoryUpdate(DirectoryScanUpdate update)
    {
        if (!_logicalNodes.TryGetValue(update.FullPath, out LiveDirectory? node))
        {
            node = new LiveDirectory(update.FullPath, update.ParentPath, update.Name);
            _logicalNodes.Add(node.FullPath, node);
        }

        node.Name = update.Name;
        node.SizeBytes = update.SizeBytes;
        node.FileCount = update.FileCount;
        node.DirectoryCount = update.DirectoryCount;
        node.LastWriteTime = update.LastWriteTime;
        node.IsComplete = update.IsComplete;

        if (update.ParentPath is not null
            && _logicalNodes.TryGetValue(update.ParentPath, out LiveDirectory? parent))
        {
            node.Parent = parent;
            if (!parent.Children.Contains(node))
            {
                parent.Children.Add(node);
            }

            if (parent.IsExpanded && !_filterActive)
            {
                EnsureVisibleChild(parent, node);
            }
        }

        node.Row?.UpdateFrom(node, _rootNode?.SizeBytes ?? 0);
    }

    private void UpdateVisibleRows()
    {
        long rootSizeBytes = _rootNode?.SizeBytes ?? 0;
        foreach (DirectoryRowViewModel row in _visibleRows)
        {
            row.UpdateFrom(row.Node, rootSizeBytes);
        }
    }

    private void ExpandRow(DirectoryRowViewModel row)
    {
        LiveDirectory node = row.Node;
        node.IsExpanded = true;
        _expandedNodes.Add(node);
        row.IsExpanded = true;

        EnsureChildrenSorted(node);
        List<DirectoryRowViewModel> children = node.Children
            .Select(child => GetOrCreateRow(child, row.Depth + 1))
            .ToList();

        int insertIndex = _visibleRows.IndexOf(row) + 1;
        _visibleRows.InsertRange(insertIndex, children);
    }

    private void CollapseRow(DirectoryRowViewModel row)
    {
        LiveDirectory node = row.Node;
        node.IsExpanded = false;
        _expandedNodes.Remove(node);
        row.IsExpanded = false;

        int startIndex = _visibleRows.IndexOf(row) + 1;
        int count = 0;
        while (startIndex + count < _visibleRows.Count
               && _visibleRows[startIndex + count].Depth > row.Depth)
        {
            DirectoryRowViewModel descendant = _visibleRows[startIndex + count];
            descendant.Node.IsExpanded = false;
            _expandedNodes.Remove(descendant.Node);
            descendant.Node.Row = null;
            _visibleRowsByPath.Remove(descendant.FullPath);
            count++;
        }

        _visibleRows.RemoveRange(startIndex, count);
    }

    private void EnsureVisibleChild(LiveDirectory parent, LiveDirectory child)
    {
        if (parent.Row is null || !parent.IsExpanded || child.Row is not null)
        {
            return;
        }

        int parentIndex = _visibleRows.IndexOf(parent.Row);
        if (parentIndex < 0)
        {
            return;
        }

        int insertIndex = parentIndex + 1;
        while (insertIndex < _visibleRows.Count
               && _visibleRows[insertIndex].Depth > parent.Row.Depth)
        {
            insertIndex++;
        }

        _visibleRows.Insert(insertIndex, GetOrCreateRow(child, parent.Row.Depth + 1));
    }

    private void SortVisibleSiblingGroups(SortColumn column, bool descending)
    {
        foreach (LiveDirectory parent in _expandedNodes.ToArray())
        {
            if (parent.Row is null || parent.Children.Count < 2)
            {
                continue;
            }

            List<LiveDirectory> sortedChildren = new(parent.Children);
            sortedChildren.Sort((left, right) => CompareDirectories(left, right, column, descending));
            bool orderChanged = parent.Children.Where((child, index) => child != sortedChildren[index]).Any();
            if (!orderChanged)
            {
                continue;
            }

            parent.Children.Sort((left, right) => CompareDirectories(left, right, column, descending));
            ReorderVisibleChildren(parent);
        }
    }

    private void ReorderVisibleChildren(LiveDirectory parent)
    {
        if (parent.Row is null)
        {
            return;
        }

        int startIndex = _visibleRows.IndexOf(parent.Row) + 1;
        if (startIndex < 1 || startIndex >= _visibleRows.Count)
        {
            return;
        }

        Dictionary<LiveDirectory, List<DirectoryRowViewModel>> blocks = [];
        LiveDirectory? currentChild = null;
        int endIndex = startIndex;
        while (endIndex < _visibleRows.Count && _visibleRows[endIndex].Depth > parent.Row.Depth)
        {
            DirectoryRowViewModel row = _visibleRows[endIndex];
            if (row.Depth == parent.Row.Depth + 1)
            {
                currentChild = row.Node;
                blocks[currentChild] = [];
            }

            if (currentChild is not null)
            {
                blocks[currentChild].Add(row);
            }

            endIndex++;
        }

        List<DirectoryRowViewModel> desiredRows = [];
        foreach (LiveDirectory child in parent.Children)
        {
            if (blocks.TryGetValue(child, out List<DirectoryRowViewModel>? block))
            {
                desiredRows.AddRange(block);
            }
        }

        int visibleCount = endIndex - startIndex;
        if (desiredRows.Count != visibleCount)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < visibleCount; i++)
        {
            if (!ReferenceEquals(_visibleRows[startIndex + i], desiredRows[i]))
            {
                changed = true;
                break;
            }
        }

        if (changed)
        {
            _visibleRows.ReplaceRange(startIndex, visibleCount, desiredRows);
        }
    }

    private readonly Dictionary<string, DirectoryRowViewModel> _visibleRowsByPath = new(StringComparer.OrdinalIgnoreCase);

    private DirectoryRowViewModel GetOrCreateRow(LiveDirectory node, int depth)
    {
        if (_visibleRowsByPath.TryGetValue(node.FullPath, out DirectoryRowViewModel? existing))
        {
            existing.Depth = depth;
            existing.UpdateFrom(node, _rootNode?.SizeBytes ?? 0);
            return existing;
        }

        DirectoryRowViewModel row = new(node, depth, _rootNode?.SizeBytes ?? 0);
        _visibleRowsByPath.Add(node.FullPath, row);
        node.Row = row;
        return row;
    }

    private DirectoryRowViewModel CreateRow(LiveDirectory node, int depth)
    {
        DirectoryRowViewModel row = GetOrCreateRow(node, depth);
        row.IsExpanded = node.IsExpanded;
        return row;
    }

    private void UpdateProgress(ScanProgress progress)
    {
        CurrentPathTextBlock.Text = progress.CurrentPath;
        FilesTextBlock.Text = progress.FilesScanned.ToString("N0", CultureInfo.CurrentCulture);
        DirectoriesTextBlock.Text = progress.DirectoriesScanned.ToString("N0", CultureInfo.CurrentCulture);
        BytesTextBlock.Text = FormatBytes(progress.BytesScanned);
    }

    private void SetScanningState(bool isScanning)
    {
        ScanButton.IsEnabled = !isScanning;
        BrowseButton.IsEnabled = !isScanning;
        OpenPathButton.IsEnabled = !isScanning;
        PathTextBox.IsEnabled = !isScanning;
        CancelButton.IsEnabled = isScanning;
        ScanProgressBar.IsIndeterminate = isScanning;
        SaveReportMenuItem.IsEnabled = !isScanning && _lastScanResult is not null;
        SaveReportAsMenuItem.IsEnabled = !isScanning && _lastScanResult is not null;
    }

    private void ResetScanDisplay()
    {
        _lastScanResult = null;
        _reportFilePath = null;
        _visibleRows.Clear();
        _visibleRowsByPath.Clear();
        _expandedNodes.Clear();
        _rootNode = null;
        CurrentPathTextBlock.Text = "-";
        FilesTextBlock.Text = "0";
        DirectoriesTextBlock.Text = "0";
        BytesTextBlock.Text = "0 B";
        DurationTextBlock.Text = "00:00:00";
        ErrorsTextBlock.Text = "0";
        ResultTextBlock.Text = LocalizationManager.GetString("StatusScanning");
        SaveReportMenuItem.IsEnabled = false;
        SaveReportAsMenuItem.IsEnabled = false;
    }

    private void ShowError(string message)
    {
        ResultTextBlock.Text = LocalizationManager.Format("ErrorPrefix", message);
        System.Windows.MessageBox.Show(this, message, LocalizationManager.GetString("DialogScanErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static int GetPathDepth(DirectoryScanUpdate update) =>
        update.FullPath.Count(character => character is '\\' or '/');

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = Math.Max(0, bytes);
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value.ToString("0.##", CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.ToString(duration.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture);

    private sealed class QueueProgressSink : IProgress<ScanProgress>
    {
        private readonly Action<ScanProgress> _enqueue;

        public QueueProgressSink(Action<ScanProgress> enqueue)
        {
            _enqueue = enqueue;
        }

        public void Report(ScanProgress value) => _enqueue(value);
    }

    public sealed class VisibleRowsCollection : ObservableCollection<DirectoryRowViewModel>
    {
        public void InsertRange(int index, IList<DirectoryRowViewModel> rows)
        {
            if (rows.Count == 0)
            {
                return;
            }

            foreach (DirectoryRowViewModel row in rows)
            {
                Items.Insert(index++, row);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void RemoveRange(int index, int count)
        {
            if (count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Items.RemoveAt(index);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void ReplaceAll(IList<DirectoryRowViewModel> rows)
        {
            Items.Clear();
            foreach (DirectoryRowViewModel row in rows)
            {
                Items.Add(row);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public void ReplaceRange(int index, int count, IList<DirectoryRowViewModel> rows)
        {
            for (int i = 0; i < count; i++)
            {
                Items.RemoveAt(index);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                Items.Insert(index + i, rows[i]);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public sealed class DirectoryRowViewModel : INotifyPropertyChanged
    {
        private readonly LiveDirectory _node;
        private int _depth;
        private bool _isExpanded;

        public DirectoryRowViewModel(LiveDirectory node, int depth, long rootSizeBytes)
        {
            _node = node;
            _depth = depth;
            UpdateFrom(node, rootSizeBytes);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LiveDirectory Node => _node;

        public string Name => _node.Name;

        public string FullPath => _node.FullPath;

        public int Depth
        {
            get => _depth;
            set
            {
                if (_depth != value)
                {
                    _depth = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(NameMargin));
                }
            }
        }

        public long SizeBytes => _node.SizeBytes;

        public long FileCount => _node.FileCount;

        public long DirectoryCount => _node.DirectoryCount;

        public double RootPercentage { get; private set; }

        public double BarRatio { get; private set; }

        public double BarWidth { get; private set; }

        public DirectoryScanState State { get; private set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExpandGlyph));
                }
            }
        }

        public bool HasChildren => _node.Children.Count > 0 || !_node.IsComplete;

        public string ExpandGlyph => HasChildren ? IsExpanded ? "▾" : "▸" : string.Empty;

        public Thickness NameMargin => new(Depth * 18, 0, 0, 0);

        public string SizeDisplay => FormatBytes(SizeBytes);

        public string FileCountDisplay => FileCount.ToString("N0", CultureInfo.CurrentCulture);

        public string DirectoryCountDisplay => DirectoryCount.ToString("N0", CultureInfo.CurrentCulture);

        public string RootPercentageDisplay => $"{RootPercentage.ToString("0.0", CultureInfo.CurrentCulture)} %";

        public void UpdateFrom(LiveDirectory node, long rootSizeBytes)
        {
            RootPercentage = node.Parent is null
                ? 100
                : rootSizeBytes > 0 ? node.SizeBytes * 100d / rootSizeBytes : 0;
            BarRatio = node.Parent is null
                ? 1
                : node.Parent.SizeBytes > 0 ? node.SizeBytes / (double)node.Parent.SizeBytes : 0;
            BarWidth = Math.Clamp(BarRatio * 140, 0, 140);
            State = node.IsComplete ? DirectoryScanState.Complete : DirectoryScanState.Scanning;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(SizeBytes));
            OnPropertyChanged(nameof(FileCount));
            OnPropertyChanged(nameof(DirectoryCount));
            OnPropertyChanged(nameof(RootPercentage));
            OnPropertyChanged(nameof(BarRatio));
            OnPropertyChanged(nameof(BarWidth));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(ExpandGlyph));
            OnPropertyChanged(nameof(SizeDisplay));
            OnPropertyChanged(nameof(FileCountDisplay));
            OnPropertyChanged(nameof(DirectoryCountDisplay));
            OnPropertyChanged(nameof(RootPercentageDisplay));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static string FormatBytes(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
            double value = Math.Max(0, bytes);
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return $"{value.ToString("0.##", CultureInfo.CurrentCulture)} {units[unitIndex]}";
        }
    }

    public enum DirectoryScanState
    {
        Scanning,
        Complete
    }

    private enum SortColumn
    {
        Size,
        Name,
        Files,
        Directories,
        Share
    }

    public sealed class LiveDirectory
    {
        public LiveDirectory(string fullPath, string? parentPath, string name)
        {
            FullPath = fullPath;
            ParentPath = parentPath;
            Name = name;
        }

        public string FullPath { get; }

        public string? ParentPath { get; }

        public string Name { get; set; }

        public long SizeBytes { get; set; }

        public long FileCount { get; set; }

        public long DirectoryCount { get; set; }

        public DateTime? LastWriteTime { get; set; }

        public bool IsComplete { get; set; }

        public bool IsExpanded { get; set; }

        public LiveDirectory? Parent { get; set; }

        public List<LiveDirectory> Children { get; } = [];

        public DirectoryRowViewModel? Row { get; set; }

        public int SortVersion { get; set; }
    }
}
