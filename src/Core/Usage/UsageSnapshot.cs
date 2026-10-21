namespace CodexMonitor.Core.Usage;

public sealed record UsageWindow(string Name, double UsedPercent, DateTimeOffset? ResetsAt);

public sealed record UsageSnapshot(string? Plan, DateTimeOffset? RetrievedAt, IReadOnlyList<UsageWindow> Windows)
{
    public string? Status { get; init; }
    // The provider supplies its windows in priority order.
    public UsageWindow? MainWindow => Windows.FirstOrDefault();
}
