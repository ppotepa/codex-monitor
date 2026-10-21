using System.Text.Json.Serialization;

namespace CodexMonitor.Core.Usage.History;

// Single baseline data model: each sample belongs to exactly one provider.
public sealed record UsageHistoryPoint(
    DateTimeOffset At, string Name, double UsedPercent, long? ResetAt,
    [property: JsonPropertyName("pluginId")] string ProviderId);

internal sealed class UsageHistory
{
    private readonly IUsageDataStore _database;
    private readonly List<UsageHistoryPoint> _points;

    private UsageHistory(IUsageDataStore database)
    {
        _database = database;
        _points = database.ReadLimitHistory().ToList();
    }

    public static UsageHistory Load(IUsageDataStore database) => new(database);

    public bool Add(UsageSnapshot snapshot, DateTimeOffset now, string providerId)
    {
        var changed = false;
        foreach (var window in snapshot.Windows)
        {
            var last = _points.LastOrDefault(point =>
                point.Name.Equals(window.Name, StringComparison.OrdinalIgnoreCase) &&
                point.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));
            if (last is not null && now - last.At < TimeSpan.FromMinutes(4) &&
                last.UsedPercent == window.UsedPercent &&
                last.ResetAt == window.ResetsAt?.ToUnixTimeSeconds())
                continue;

            _points.Add(new UsageHistoryPoint(
                now, window.Name, window.UsedPercent, window.ResetsAt?.ToUnixTimeSeconds(), providerId));
            changed = true;
        }

        var removed = _points.RemoveAll(point => point.At < now.AddDays(-7) || point.At > now.AddMinutes(5));
        return changed || removed > 0;
    }

    public IReadOnlyList<UsageHistoryPoint> Recent(string? name, DateTimeOffset now, string providerId) =>
        name is null ? [] : _points.Where(point =>
            point.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            point.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase) &&
            point.At >= now.AddHours(-24) && point.At <= now)
            .OrderBy(point => point.At).ToArray();

    public void Save() => _database.ReplaceLimitHistory(_points);
}
