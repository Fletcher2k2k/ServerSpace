using System.Globalization;
using System.IO;
using System.Text;
using ServerSpace.Core.Models;
using ServerSpace.UI.Localization;

namespace ServerSpace.UI.Services;

public sealed class ReportExporter
{
    public static string CreateSuggestedFileName(string scanPath, DateTime startedAt)
    {
        string safePath = string.Join(
            "_",
            scanPath.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        safePath = safePath.Replace('\\', '_').Replace('/', '_').Replace(':', '_');
        safePath = string.IsNullOrWhiteSpace(safePath) ? "Scan" : safePath;
        return $"ServerSpace_{safePath}_{startedAt:yyyy-MM-dd_HHmm}";
    }

    public void Export(ScanResult scanResult, string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            ExportText(scanResult, filePath);
            return;
        }

        ExportCsv(scanResult, filePath);
    }

    private static void ExportCsv(ScanResult scanResult, string filePath)
    {
        using StreamWriter writer = CreateWriter(filePath);
        writer.WriteLine(LocalizationManager.GetString("ReportCsvHeader"));

        WriteCsvDirectory(writer, scanResult, scanResult.Root, null, 0, scanResult.Root.SizeBytes);

        if (scanResult.Errors.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine(LocalizationManager.GetString("ReportErrors"));
            foreach (ScanError error in scanResult.Errors)
            {
                writer.WriteLine(string.Join(';', Csv(error.Path), Csv(error.Message)));
            }
        }
    }

    private static void WriteCsvDirectory(
        StreamWriter writer,
        ScanResult scanResult,
        DirectoryNode node,
        string? parentPath,
        int depth,
        long rootSizeBytes)
    {
        double percentage = parentPath is null
            ? 100
            : rootSizeBytes > 0 ? node.SizeBytes * 100d / rootSizeBytes : 0;
        string status = LocalizationManager.GetString(scanResult.WasCancelled ? "ReportStatusCancelled" : "ReportStatusOk");
        string lastWriteTime = node.LastWriteTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

        writer.WriteLine(string.Join(';',
            Csv(node.FullPath),
            Csv(parentPath ?? string.Empty),
            Csv(node.Name),
            depth.ToString(CultureInfo.InvariantCulture),
            Csv(FormatBytes(node.SizeBytes)),
            node.SizeBytes.ToString(CultureInfo.InvariantCulture),
            node.FileCount.ToString(CultureInfo.InvariantCulture),
            node.DirectoryCount.ToString(CultureInfo.InvariantCulture),
            percentage.ToString("0.0", CultureInfo.InvariantCulture),
            Csv(status),
            Csv(lastWriteTime)));

        foreach (DirectoryNode child in SortedChildren(node))
        {
            WriteCsvDirectory(writer, scanResult, child, node.FullPath, depth + 1, rootSizeBytes);
        }
    }

    private static void ExportText(ScanResult scanResult, string filePath)
    {
        using StreamWriter writer = CreateWriter(filePath);
        string status = LocalizationManager.GetString(scanResult.WasCancelled ? "ReportCancelledStatus" : "ReportCompletedStatus");

        writer.WriteLine(LocalizationManager.GetString("ReportHeader"));
        writer.WriteLine();
        writer.WriteLine($"{LocalizationManager.GetString("ReportScanPath")}: {scanResult.Root.FullPath}");
        writer.WriteLine($"{LocalizationManager.GetString("ReportStart")}: {scanResult.StartedAt:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"{LocalizationManager.GetString("ReportEnd")}: {scanResult.FinishedAt:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"{LocalizationManager.GetString("ReportDuration")}: {scanResult.Duration}");
        writer.WriteLine($"{LocalizationManager.GetString("ReportStatus")}: {status}");
        writer.WriteLine($"{LocalizationManager.GetString("ReportTotalSize")}: {FormatBytes(scanResult.TotalBytes)}");
        writer.WriteLine($"{LocalizationManager.GetString("StatFiles")}: {scanResult.TotalFiles.ToString("N0", CultureInfo.CurrentCulture)}");
        writer.WriteLine($"{LocalizationManager.GetString("StatFolders")}: {scanResult.TotalDirectories.ToString("N0", CultureInfo.CurrentCulture)}");
        writer.WriteLine($"{LocalizationManager.GetString("ReportErrorCount")}: {scanResult.Errors.Count.ToString("N0", CultureInfo.CurrentCulture)}");
        writer.WriteLine();
        writer.WriteLine(LocalizationManager.GetString("ReportDirectoryOverview"));
        writer.WriteLine();

        WriteTextDirectory(writer, scanResult.Root, 0);

        if (scanResult.Errors.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine(LocalizationManager.GetString("ReportErrors"));
            foreach (ScanError error in scanResult.Errors)
            {
                writer.WriteLine($"{LocalizationManager.GetString("ReportErrorPathLabel")}: {error.Path}");
                writer.WriteLine($"{LocalizationManager.GetString("ReportErrorMessageLabel")}: {error.Message}");
                writer.WriteLine();
            }
        }
    }

    private static void WriteTextDirectory(StreamWriter writer, DirectoryNode node, int depth)
    {
        writer.WriteLine($"{new string(' ', depth * 2)}{node.Name}  {FormatBytes(node.SizeBytes)}");
        foreach (DirectoryNode child in SortedChildren(node))
        {
            WriteTextDirectory(writer, child, depth + 1);
        }
    }

    private static IEnumerable<DirectoryNode> SortedChildren(DirectoryNode node)
    {
        List<DirectoryNode> children = new(node.Children);
        children.Sort((left, right) =>
        {
            int comparison = right.SizeBytes.CompareTo(left.SizeBytes);
            return comparison != 0
                ? comparison
                : StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
        });
        return children;
    }

    private static StreamWriter CreateWriter(string filePath) =>
        new(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

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
