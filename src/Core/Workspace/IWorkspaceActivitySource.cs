namespace CodexMonitor.Core.Workspace;

public sealed record WorkspaceActivityEvent(
    DateTimeOffset At,
    string ProviderId,
    string EventType,
    string? ProjectDirectory,
    string? SessionId,
    string Origin);

public sealed record WorkspaceActivitySnapshot(
    IReadOnlyList<WorkspaceActivityEvent> Events,
    bool IsConfigured,
    string Coverage,
    DateTimeOffset? LastObservedAt);

public interface IWorkspaceActivitySource
{
    WorkspaceActivitySnapshot ReadRecent(string? projectDirectory, int maxEvents = 80);
}
