using System.Text.Json;

namespace CodexMonitor;

// PluginId defaults to Codex when reading legacy 4-field history JSON.
internal sealed record UsageHistoryPoint(DateTimeOffset At, string Name, double UsedPercent,
    long? ResetAt, string PluginId = "codex");

internal sealed class UsageHistory
{
    private readonly List<UsageHistoryPoint> _points;
    private readonly string _path;
    private readonly MonitorDatabase? _database;

    internal UsageHistory(string path, IEnumerable<UsageHistoryPoint>? points = null)
    {
        _path = path;
        _points = points?.ToList() ?? [];
    }

    private UsageHistory(MonitorDatabase database)
    {
        _database = database;
        _path = string.Empty;
        _points = database.ReadLimitHistory().ToList();
    }

    internal MonitorDatabase? Database => _database;

    public static UsageHistory Load()
        => new(MonitorDatabase.Load());

    public static UsageHistory Load(MonitorDatabase database) => new(database);

    public bool Add(UsageSnapshot snapshot, DateTimeOffset now, string pluginId = "codex")
    {
        var changed = false;
        foreach (var window in snapshot.Windows)
        {
            var last = _points.LastOrDefault(p => p.Name.Equals(window.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
            if (last is not null && now - last.At < TimeSpan.FromMinutes(4) &&
                last.UsedPercent == window.UsedPercent && last.ResetAt == window.ResetsAt?.ToUnixTimeSeconds())
                continue;
            _points.Add(new UsageHistoryPoint(now, window.Name, window.UsedPercent, window.ResetsAt?.ToUnixTimeSeconds(), pluginId));
            changed = true;
        }
        var removed = _points.RemoveAll(p => p.At < now.AddDays(-7) || p.At > now.AddMinutes(5));
        return changed || removed > 0;
    }

    public IReadOnlyList<UsageHistoryPoint> Recent(string? name, DateTimeOffset now, string pluginId = "codex") =>
        name is null ? [] : _points.Where(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) &&
            p.At >= now.AddHours(-24) && p.At <= now).OrderBy(p => p.At).ToArray();

    public void Save()
    {
        if (_database is not null)
        {
            _database.ReplaceLimitHistory(_points);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_points));
        File.Move(temporary, _path, true);
    }
}

