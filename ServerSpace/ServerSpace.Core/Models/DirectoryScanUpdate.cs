namespace ServerSpace.Core.Models;

public class DirectoryScanUpdate
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public string? ParentPath { get; init; }

    public long SizeBytes { get; init; }

    public long FileCount { get; init; }

    public long DirectoryCount { get; init; }

    public DateTime? LastWriteTime { get; init; }

    public bool IsComplete { get; init; }
}
