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
    private string? _persistenceError;
    private bool _preserveExistingBackup;

    internal MonitorDatabase(string path, MonitorDatabaseDocument? document = null)
    {
        _path = path;
        _document = document ?? new MonitorDatabaseDocument();
        _document.LimitHistory ??= [];
        _document.TokenCache ??= new Dictionary<string, TokenCacheEntry>(StringComparer.OrdinalIgnoreCase);
        _document.TokenCache = new Dictionary<string, TokenCacheEntry>(_document.TokenCache, StringComparer.OrdinalIgnoreCase);
    }

    public string? PersistenceError
    {
        get { lock (_gate) return _persistenceError; }
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
        if (!File.Exists(path)) return new MonitorDatabase(path);
        try
        {
            var document = JsonSerializer.Deserialize<MonitorDatabaseDocument>(File.ReadAllText(path), JsonOptions)
                ?? new MonitorDatabaseDocument();
            return new MonitorDatabase(path, document);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            var database = new MonitorDatabase(path);
            database._persistenceError = PreserveCorruptFile(path, ex);
            var backup = path + ".bak";
            if (File.Exists(backup))
            {
                try
                {
                    var recovered = JsonSerializer.Deserialize<MonitorDatabaseDocument>(File.ReadAllText(backup), JsonOptions);
                    if (recovered is not null)
                    {
                        database._document = new MonitorDatabase(path, recovered)._document;
                        database._preserveExistingBackup = true;
                        database._persistenceError += " Recovered the last valid local cache.";
                    }
                }
                catch (Exception backupError) when (backupError is IOException or JsonException or UnauthorizedAccessException) { }
            }
            return database;
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
            var candidate = CloneDocument();
            candidate.LimitHistory = points.ToList();
            SaveLocked(candidate);
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
            var candidate = CloneDocument();
            candidate.TokenCache[pluginId] = new TokenCacheEntry(cachedAt, snapshot);
            SaveLocked(candidate);
        }
    }

    private MonitorDatabaseDocument CloneDocument() => new()
    {
        Version = _document.Version,
        LimitHistory = _document.LimitHistory.ToList(),
        TokenCache = new Dictionary<string, TokenCacheEntry>(_document.TokenCache, StringComparer.OrdinalIgnoreCase)
    };

    private void SaveLocked(MonitorDatabaseDocument candidate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(candidate, JsonOptions));
            // Keep a known-good recovery copy when the active file was loaded
            // from .bak after corruption. The original bad file is already in
            // a timestamped .corrupt.* copy.
            if (File.Exists(_path) && !_preserveExistingBackup)
                File.Copy(_path, _path + ".bak", true);
            File.Move(temporary, _path, true);
            _document = candidate;
            _persistenceError = null;
            _preserveExistingBackup = false;
        }
        finally
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static string PreserveCorruptFile(string path, Exception error)
    {
        var backupPath = $"{path}.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            File.Copy(path, backupPath, false);
            return $"Local cache was unreadable and was preserved as {Path.GetFileName(backupPath)}.";
        }
        catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException)
        {
            return $"Local cache was unreadable ({error.Message}) and could not be backed up.";
        }
    }
}

internal sealed class MonitorDatabaseDocument
{
    public int Version { get; set; } = 1;
    public List<UsageHistoryPoint> LimitHistory { get; set; } = [];
    public Dictionary<string, TokenCacheEntry> TokenCache { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed record TokenCacheEntry(DateTimeOffset CachedAt, TokenUsageSnapshot Snapshot);
