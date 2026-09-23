namespace ServerSpace.Core.Models;

public class ScanResult
{
    public required DirectoryNode Root { get; init; }

    public DateTime StartedAt { get; init; }

    public DateTime FinishedAt { get; init; }

    public TimeSpan Duration { get; init; }

    public long TotalBytes { get; init; }

    public long TotalFiles { get; init; }

    public long TotalDirectories { get; init; }

    public List<ScanError> Errors { get; init; } = [];

    public bool WasCancelled { get; init; }
}
