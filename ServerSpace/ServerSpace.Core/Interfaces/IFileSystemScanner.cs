using ServerSpace.Core.Models;

namespace ServerSpace.Core.Interfaces;

public interface IFileSystemScanner
{
    Task<ScanResult> ScanAsync(
        string path,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
