namespace ServerSpace.Core.Models;

public class DirectoryNode
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public long SizeBytes { get; init; }

    public long FileCount { get; init; }

    public long DirectoryCount { get; init; }

    public DateTime? LastWriteTime { get; init; }

    public List<DirectoryNode> Children { get; init; } = [];
}
