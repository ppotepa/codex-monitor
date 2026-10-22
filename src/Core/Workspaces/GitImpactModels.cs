namespace CodexMonitor.Core.Workspaces;

public sealed record GitImpactFile(string Path, int AddedLines, int RemovedLines, string ChangeType);

public sealed record GitImpactSnapshot(
    AvailabilityStatus Status,
    string ProjectPath,
    string Summary,
    string? Branch,
    int ChangedFiles,
    int UntrackedFiles,
    int AddedLines,
    int RemovedLines,
    IReadOnlyList<GitImpactFile> Files,
    DateTimeOffset ObservedAt,
    string? Error = null);
