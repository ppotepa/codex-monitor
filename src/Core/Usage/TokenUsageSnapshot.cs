namespace CodexMonitor.Core.Usage;

public sealed record DailyTokenUsage(DateOnly Date, long Tokens);

public sealed record TokenUsageSnapshot(
    DateTimeOffset RetrievedAt,
    long? LifetimeTokens,
    long? PeakDailyTokens,
    int? CurrentStreakDays,
    IReadOnlyList<DailyTokenUsage> DailyBuckets);
