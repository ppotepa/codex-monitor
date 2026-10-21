namespace CodexMonitor;

internal sealed record UsageWindow(string Name, double UsedPercent, DateTimeOffset? ResetsAt);

internal sealed record UsageSnapshot(string? Plan, DateTimeOffset? RetrievedAt, IReadOnlyList<UsageWindow> Windows)
{
    public string? Status { get; init; }
    // The plugin supplies its windows in priority order.
    public UsageWindow? MainWindow => Windows.FirstOrDefault();
}
