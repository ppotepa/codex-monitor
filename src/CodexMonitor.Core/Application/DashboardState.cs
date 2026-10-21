namespace CodexMonitor;

// Immutable, OS-independent snapshot consumed by desktop presentations.
internal sealed record DashboardState(
    UsageSnapshot? Limits,
    UsageWindow? SelectedLimit,
    IReadOnlyList<UsageHistoryPoint> RecentHistory,
    string? LimitError,
    bool LimitsBusy,
    DependencyProblem DependencyProblem,
    TokenUsageSnapshot? Tokens,
    string? TokenError,
    bool TokensBusy,
    PluginStatus PluginStatus);
