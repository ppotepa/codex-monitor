namespace CodexMonitor.Core.Workspaces;

public interface IGitImpactService
{
    Task<GitImpactSnapshot> ReadAsync(string? projectPath, CancellationToken cancellationToken = default);
}
