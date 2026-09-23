namespace ServerSpace.Core.Models;

public class ScanProgress
{
    public required string CurrentPath { get; init; }

    public long FilesScanned { get; init; }

    public long DirectoriesScanned { get; init; }

    public long BytesScanned { get; init; }

    public IReadOnlyList<DirectoryScanUpdate> DirectoryUpdates { get; init; } = [];
}
