namespace CodexMonitor.Core.Usage.History;

public interface IUsageDataStore
{
    string? PersistenceError { get; }
    IReadOnlyList<UsageHistoryPoint> ReadLimitHistory();
    void ReplaceLimitHistory(IEnumerable<UsageHistoryPoint> points);
    TokenCacheEntry? ReadTokenUsage(string providerId);
    void SaveTokenUsage(string providerId, TokenUsageSnapshot snapshot, DateTimeOffset cachedAt);
}

public sealed record TokenCacheEntry(DateTimeOffset CachedAt, TokenUsageSnapshot Snapshot);
