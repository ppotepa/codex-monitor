using System.Text.Json;

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
    public Dictionary<string, WindowAlertState> AlertStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    private string? _filePath;

    public static MonitorSettings Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var path = filePath;
        try
        {
            var settings = JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(path)) ?? new MonitorSettings();
            settings._filePath = path;
            settings.AlertStates ??= new Dictionary<string, WindowAlertState>(StringComparer.OrdinalIgnoreCase);
            settings.AlertStates = new Dictionary<string, WindowAlertState>(settings.AlertStates, StringComparer.OrdinalIgnoreCase);
            if (settings.AlertStates.Any(pair =>
                pair.Key.IndexOf(':') <= 0 || pair.Key.EndsWith(':') ||
                pair.Value is null || pair.Value.Level is < 0 or > 2))
                throw new JsonException("Invalid alert state: expected plugin-scoped keys and levels 0-2.");
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
        catch (IOException) { return new MonitorSettings { _filePath = path }; }
        catch (JsonException) { return new MonitorSettings { _filePath = path }; }
        catch (UnauthorizedAccessException) { return new MonitorSettings { _filePath = path }; }
    }

    public void Save()
    {
        var path = _filePath ?? throw new InvalidOperationException(
            "Settings must be loaded with an explicit data directory.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, path, overwrite: true);
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
