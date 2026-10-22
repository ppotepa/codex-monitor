namespace CodexMonitor.Core.Workspace;

// Git's line counts describe the repository diff, not code authored by any agent.
public sealed record GitFileImpact(string Path, int Added, int Deleted, bool Binary);
public sealed record GitImpactSnapshot(
    string RepositoryRoot,
    string Branch,
    int AddedLines,
    int DeletedLines,
    int UntrackedFiles,
    IReadOnlyList<GitFileImpact> Files,
    string LatestCommit,
    bool IsWorktree);

public enum GitImpactScope { WorkingTree, LastCommit }

public interface IGitImpactService
{
    Task<GitImpactSnapshot> InspectAsync(string directory, CancellationToken cancellationToken = default);
    Task<GitImpactSnapshot> InspectRangeAsync(string directory, GitImpactScope scope,
        CancellationToken cancellationToken = default);
}
