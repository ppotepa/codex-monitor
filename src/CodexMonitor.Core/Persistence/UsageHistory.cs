namespace CodexMonitor;

// Single baseline data model: each sample belongs to exactly one plugin.
internal sealed record UsageHistoryPoint(
    DateTimeOffset At, string Name, double UsedPercent, long? ResetAt, string PluginId);

internal sealed class UsageHistory
{
    private readonly MonitorDatabase _database;
    private readonly List<UsageHistoryPoint> _points;

    private UsageHistory(MonitorDatabase database)
    {
        _database = database;
        _points = database.ReadLimitHistory().ToList();
    }

    public static UsageHistory Load(MonitorDatabase database) => new(database);

    public bool Add(UsageSnapshot snapshot, DateTimeOffset now, string pluginId)
    {
        var changed = false;
        foreach (var window in snapshot.Windows)
        {
            var last = _points.LastOrDefault(point =>
                point.Name.Equals(window.Name, StringComparison.OrdinalIgnoreCase) &&
                point.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase));
            if (last is not null && now - last.At < TimeSpan.FromMinutes(4) &&
                last.UsedPercent == window.UsedPercent &&
                last.ResetAt == window.ResetsAt?.ToUnixTimeSeconds())
                continue;

            _points.Add(new UsageHistoryPoint(
                now, window.Name, window.UsedPercent, window.ResetsAt?.ToUnixTimeSeconds(), pluginId));
            changed = true;
        }

        var removed = _points.RemoveAll(point => point.At < now.AddDays(-7) || point.At > now.AddMinutes(5));
        return changed || removed > 0;
    }

    public IReadOnlyList<UsageHistoryPoint> Recent(string? name, DateTimeOffset now, string pluginId) =>
        name is null ? [] : _points.Where(point =>
            point.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            point.PluginId.Equals(pluginId, StringComparison.OrdinalIgnoreCase) &&
            point.At >= now.AddHours(-24) && point.At <= now)
            .OrderBy(point => point.At).ToArray();

    public void Save() => _database.ReplaceLimitHistory(_points);
}
