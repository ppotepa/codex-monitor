using System.Text.Json;

namespace CodexMonitor;

// A deliberately small, file-backed store. It keeps the app self-contained and
// gives every provider one place for cached data without introducing a database
// server or a native SQLite payload.
internal sealed class MonitorDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly object _gate = new();
    private readonly string _path;
    private MonitorDatabaseDocument _document;

    internal MonitorDatabase(string path, MonitorDatabaseDocument? document = null)
    {
        _path = path;
        _document = document ?? new MonitorDatabaseDocument();
        _document.LimitHistory ??= [];
        _document.TokenCache ??= new Dictionary<string, TokenCacheEntry>(StringComparer.OrdinalIgnoreCase);
        _document.TokenCache = new Dictionary<string, TokenCacheEntry>(_document.TokenCache, StringComparer.OrdinalIgnoreCase);
    }

    public static MonitorDatabase Load(string? filePath = null)
    {
        var path = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexMonitor", "monitor.db.json");
        var database = Open(path);
        if (File.Exists(path)) return database;

        // Keep the existing seven-day chart when upgrading from pre-database
        // builds. The legacy file is never deleted; it remains a safe fallback.
        var legacyPath = Path.Combine(Path.GetDirectoryName(path)!, "history.json");
        try
        {
            var legacy = JsonSerializer.Deserialize<List<UsageHistoryPoint>>(
                File.ReadAllText(legacyPath), JsonOptions);
            if (legacy is { Count: > 0 }) database.ReplaceLimitHistory(legacy);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        return database;
    }

    internal static MonitorDatabase Open(string path)
    {
        try
        {
            var document = JsonSerializer.Deserialize<MonitorDatabaseDocument>(File.ReadAllText(path), JsonOptions)
                ?? new MonitorDatabaseDocument();
            return new MonitorDatabase(path, document);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new MonitorDatabase(path);
        }
    }

    public IReadOnlyList<UsageHistoryPoint> ReadLimitHistory()
    {
        lock (_gate) return _document.LimitHistory.ToArray();
    }

    public void ReplaceLimitHistory(IEnumerable<UsageHistoryPoint> points)
    {
        lock (_gate)
        {
            _document.LimitHistory = points.ToList();
            SaveLocked();
        }
    }

    public TokenCacheEntry? ReadTokenUsage(string pluginId)
    {
        lock (_gate)
            return _document.TokenCache.TryGetValue(pluginId, out var entry) ? entry : null;
    }

    public void SaveTokenUsage(string pluginId, TokenUsageSnapshot snapshot, DateTimeOffset cachedAt)
    {
        lock (_gate)
        {
            _document.TokenCache[pluginId] = new TokenCacheEntry(cachedAt, snapshot);
            SaveLocked();
        }
    }

    private void SaveLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(_document, JsonOptions));
        File.Move(temporary, _path, true);
    }
}

internal sealed class MonitorDatabaseDocument
{
    public int Version { get; set; } = 1;
    public List<UsageHistoryPoint> LimitHistory { get; set; } = [];
    public Dictionary<string, TokenCacheEntry> TokenCache { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record TokenCacheEntry(DateTimeOffset CachedAt, TokenUsageSnapshot Snapshot);
