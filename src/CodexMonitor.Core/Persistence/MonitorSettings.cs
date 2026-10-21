using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexMonitor;

internal sealed class MonitorSettings
{
    public string TrayWindow { get; set; } = "auto";
    public string PreferredAgentId { get; set; } = "auto";
    public bool AlertsEnabled { get; set; } = true;
    public int WarningThreshold { get; set; } = 80;
    public int CriticalThreshold { get; set; } = 95;
    public int LimitsRefreshMinutes { get; set; } = 5;
    public int TokenRefreshMinutes { get; set; } = 30;
    public int AgentScanMinutes { get; set; } = 30;
    public bool AutoRefreshTokens { get; set; } = true;
    public Dictionary<string, WindowAlertState> AlertStates { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Diagnostics and path ownership are runtime-only, not part of settings.json.
    [JsonIgnore]
    public string? PersistenceError { get; private set; }

    private string? _filePath;
    private bool _saveBlocked;

    public static MonitorSettings Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            var settings = JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(filePath))
                ?? throw new JsonException("Settings JSON is empty.");
            settings._filePath = filePath;
            if (settings.AlertStates is null ||
                settings.AlertStates.Any(pair =>
                    pair.Key.IndexOf(':') <= 0 || pair.Key.EndsWith(':') ||
                    pair.Value is null || pair.Value.Level is < 0 or > 2))
                throw new JsonException("Invalid settings format: expected plugin-scoped keys and levels 0-2.");

            settings.AlertStates = new Dictionary<string, WindowAlertState>(
                settings.AlertStates, StringComparer.OrdinalIgnoreCase);
            settings.TrayWindow ??= "auto";
            settings.PreferredAgentId ??= "auto";
            if (settings.WarningThreshold < 1 || settings.WarningThreshold >= 100) settings.WarningThreshold = 80;
            if (settings.CriticalThreshold <= settings.WarningThreshold || settings.CriticalThreshold > 100)
            {
                settings.WarningThreshold = 80;
                settings.CriticalThreshold = 95;
            }
            if (settings.LimitsRefreshMinutes is not (1 or 5 or 10 or 15 or 30 or 60))
                settings.LimitsRefreshMinutes = 5;
            if (settings.TokenRefreshMinutes is not (5 or 15 or 30 or 60 or 120))
                settings.TokenRefreshMinutes = 30;
            if (settings.AgentScanMinutes is not (0 or 15 or 30 or 60))
                settings.AgentScanMinutes = 30;
            return settings;
        }
        catch (FileNotFoundException) { return new MonitorSettings { _filePath = filePath }; }
        catch (DirectoryNotFoundException) { return new MonitorSettings { _filePath = filePath }; }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return RecoverUnreadable(filePath, ex);
        }
    }

    private static MonitorSettings RecoverUnreadable(string path, Exception error)
    {
        var settings = new MonitorSettings { _filePath = path };
        var backup = $"{path}.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            File.Copy(path, backup, overwrite: false);
            settings.PersistenceError =
                $"Settings could not be loaded ({error.Message}); the original was preserved as {Path.GetFileName(backup)}. Defaults are being used.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Refuse to overwrite the original if no safe copy could be kept.
            settings._saveBlocked = true;
            settings.PersistenceError =
                $"Settings could not be loaded ({error.Message}) or preserved ({ex.Message}). Saving is blocked to protect the original file.";
        }
        return settings;
    }

    public void Save()
    {
        var path = _filePath ?? throw new InvalidOperationException(
            "Settings must be loaded with an explicit data directory.");
        if (_saveBlocked) throw new IOException(
            "Cannot save settings because the unreadable original could not be preserved.");

        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, path, overwrite: true);
            PersistenceError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            PersistenceError = $"Settings could not be saved: {ex.Message}";
            throw;
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
}

internal sealed record WindowAlertState(long? ResetAt, int Level, bool Snoozed = false);
