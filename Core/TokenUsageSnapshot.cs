namespace CodexMonitor;

internal sealed record DailyTokenUsage(DateOnly Date, long Tokens);

internal sealed record TokenUsageSnapshot(
    DateTimeOffset RetrievedAt,
    long? LifetimeTokens,
    long? PeakDailyTokens,
    int? CurrentStreakDays,
    IReadOnlyList<DailyTokenUsage> DailyBuckets);
