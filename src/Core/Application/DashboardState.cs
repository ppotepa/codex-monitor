namespace CodexMonitor.Core.Application;

// Immutable, OS-independent snapshot consumed by desktop presentations.
public sealed record DashboardState(
    UsageSnapshot? Limits,
    UsageWindow? SelectedLimit,
    IReadOnlyList<UsageHistoryPoint> RecentHistory,
    string? LimitError,
    bool LimitsBusy,
    DependencyProblem DependencyProblem,
    TokenUsageSnapshot? Tokens,
    string? TokenError,
    bool TokensBusy,
    ProviderStatus ProviderStatus,
    string? PersistenceError = null);
