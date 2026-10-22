namespace CodexMonitor.Core.Workspace;

// Stored session metadata is not proof that an external host is actively running.
public sealed record AgentSessionInfo(string Id, string ProviderId, string? ProjectDirectory,
    string DisplayName, string Status, DateTimeOffset? UpdatedAt);
public sealed record AgentSessionSnapshot(IReadOnlyList<AgentSessionInfo> Sessions, string Coverage);

public interface IAgentSessionSource : IFeature
{
    string ProviderId { get; }
    Task<AgentSessionSnapshot> ListAsync(CancellationToken cancellationToken);
}
