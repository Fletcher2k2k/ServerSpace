using System.Diagnostics;
using System.Security;
using ServerSpace.Core.Interfaces;
using ServerSpace.Core.Models;

namespace ServerSpace.Scanner;

public sealed class WindowsFileSystemScanner : IFileSystemScanner
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(125);

    public Task<ScanResult> ScanAsync(
        string path,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Der Startpfad darf nicht leer sein.", nameof(path));
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            throw new ArgumentException($"Der Startpfad ist ungültig: {path}", nameof(path), exception);
        }

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Der Startpfad wurde nicht gefunden oder ist kein Ordner: {fullPath}");
        }

        return Task.Run(
            () => ScanCore(fullPath, progress, cancellationToken),
            CancellationToken.None);
    }

    private static ScanResult ScanCore(
        string rootPath,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        DateTime startedAt = DateTime.UtcNow;
        Stopwatch stopwatch = Stopwatch.StartNew();
        List<ScanError> errors = [];
        ScanCounters counters = new();
        ProgressReporter progressReporter = new(progress, counters);
        List<DirectoryFrame> frames = [];
        DirectoryNode root;
        bool wasCancelled = false;

        try
        {
            root = ScanDirectory(rootPath, null, frames, counters, errors, progressReporter, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            root = CreateDirectoryNode(rootPath, errors);
        }

        wasCancelled |= cancellationToken.IsCancellationRequested;
        stopwatch.Stop();
        DateTime finishedAt = DateTime.UtcNow;
        progressReporter.Flush(rootPath, force: true);

        return new ScanResult
        {
            Root = root,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Duration = stopwatch.Elapsed,
            TotalBytes = counters.BytesScanned,
            TotalFiles = counters.FilesScanned,
            TotalDirectories = root.DirectoryCount,
            Errors = errors,
            WasCancelled = wasCancelled
        };
    }

    private static DirectoryNode ScanDirectory(
        string path,
        string? parentPath,
        List<DirectoryFrame> frames,
        ScanCounters counters,
        List<ScanError> errors,
        ProgressReporter progressReporter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DirectoryNode metadata = CreateDirectoryNode(path, errors);
        DirectoryFrame frame = new(path, parentPath, metadata.Name);
        frames.Add(frame);
        counters.DirectoriesScanned++;

        try
        {
            frame.LastWriteTime = new DirectoryInfo(path).LastWriteTimeUtc;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            AddError(errors, path, exception);
        }

        for (int i = 0; i < frames.Count - 1; i++)
        {
            frames[i].DirectoryCount++;
        }

        progressReporter.Capture(frames, path);
        List<DirectoryNode> children = [];

        try
        {
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(path))
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entryPath);
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    AddError(errors, entryPath, exception);
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    DirectoryNode child = ScanDirectory(
                        entryPath,
                        path,
                        frames,
                        counters,
                        errors,
                        progressReporter,
                        cancellationToken);
                    children.Add(child);
                    progressReporter.Capture(frames, path);
                    continue;
                }

                counters.FilesScanned++;
                long fileSize = 0;
                try
                {
                    fileSize = new FileInfo(entryPath).Length;
                }
                catch (Exception exception) when (IsFileSystemException(exception))
                {
                    AddError(errors, entryPath, exception);
                }

                counters.BytesScanned += fileSize;
                foreach (DirectoryFrame activeFrame in frames)
                {
                    activeFrame.SizeBytes += fileSize;
                    activeFrame.FileCount++;
                }
                progressReporter.Capture(frames, path);
            }
        }
        catch (OperationCanceledException)
        {
            // Return the data collected so far. The final result marks the scan as cancelled.
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            AddError(errors, path, exception);
        }

        frame.IsComplete = true;
        progressReporter.Capture(frames, path, completeCurrent: true);
        frames.RemoveAt(frames.Count - 1);

        return new DirectoryNode
        {
            Name = frame.Name,
            FullPath = frame.FullPath,
            SizeBytes = frame.SizeBytes,
            FileCount = frame.FileCount,
            DirectoryCount = frame.DirectoryCount,
            LastWriteTime = frame.LastWriteTime,
            Children = children
        };
    }

    private static DirectoryNode CreateDirectoryNode(string path, List<ScanError> errors)
    {
        string name;
        try
        {
            name = new DirectoryInfo(path).Name;
            if (string.IsNullOrEmpty(name))
            {
                name = path;
            }
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            AddError(errors, path, exception);
            name = path;
        }

        return new DirectoryNode { Name = name, FullPath = path };
    }

    private static void AddError(List<ScanError> errors, string path, Exception exception)
    {
        errors.Add(new ScanError { Path = path, Message = exception.Message });
    }

    private static bool IsFileSystemException(Exception exception) =>
        exception is UnauthorizedAccessException
        or IOException
        or SecurityException
        or ArgumentException
        or NotSupportedException;

    private sealed class ScanCounters
    {
        public long FilesScanned { get; set; }

        public long DirectoriesScanned { get; set; }

        public long BytesScanned { get; set; }
    }

    private sealed class DirectoryFrame
    {
        public DirectoryFrame(string fullPath, string? parentPath, string name)
        {
            FullPath = fullPath;
            ParentPath = parentPath;
            Name = name;
        }

        public string FullPath { get; }

        public string? ParentPath { get; }

        public string Name { get; }

        public long SizeBytes { get; set; }

        public long FileCount { get; set; }

        public long DirectoryCount { get; set; }

        public DateTime? LastWriteTime { get; set; }

        public bool IsComplete { get; set; }
    }

    private sealed class ProgressReporter
    {
        private readonly IProgress<ScanProgress>? _progress;
        private readonly ScanCounters _counters;
        private readonly Dictionary<string, DirectoryScanUpdate> _pendingUpdates = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public ProgressReporter(IProgress<ScanProgress>? progress, ScanCounters counters)
        {
            _progress = progress;
            _counters = counters;
        }

        public void Capture(
            IReadOnlyList<DirectoryFrame> frames,
            string currentPath,
            bool completeCurrent = false,
            bool forceFlush = false)
        {
            if (_progress is null)
            {
                return;
            }

            if (completeCurrent && frames.Count > 0)
            {
                frames[^1].IsComplete = true;
                _pendingUpdates[frames[^1].FullPath] = CreateUpdate(frames[^1]);
            }

            if (!forceFlush && _stopwatch.Elapsed < ProgressInterval)
            {
                return;
            }

            foreach (DirectoryFrame frame in frames)
            {
                _pendingUpdates[frame.FullPath] = CreateUpdate(frame);
            }

            Flush(currentPath, force: true);
        }

        public void Flush(string currentPath, bool force)
        {
            if (_progress is null || (!force && _stopwatch.Elapsed < ProgressInterval))
            {
                return;
            }

            DirectoryScanUpdate[] updates = _pendingUpdates.Values.ToArray();
            _pendingUpdates.Clear();
            _progress.Report(new ScanProgress
            {
                CurrentPath = currentPath,
                FilesScanned = _counters.FilesScanned,
                DirectoriesScanned = _counters.DirectoriesScanned,
                BytesScanned = _counters.BytesScanned,
                DirectoryUpdates = updates
            });
            _stopwatch.Restart();
        }

        private static DirectoryScanUpdate CreateUpdate(DirectoryFrame frame) => new()
        {
            Name = frame.Name,
            FullPath = frame.FullPath,
            ParentPath = frame.ParentPath,
            SizeBytes = frame.SizeBytes,
            FileCount = frame.FileCount,
            DirectoryCount = frame.DirectoryCount,
            LastWriteTime = frame.LastWriteTime,
            IsComplete = frame.IsComplete
        };
    }
}
