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

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexMonitor", "settings.json");

    private string _filePath = FilePath;

    public static MonitorSettings Load(string? filePath = null)
    {
        var path = filePath ?? FilePath;
        try
        {
            var settings = JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(path)) ?? new MonitorSettings();
            settings._filePath = path;
            settings.AlertStates ??= new Dictionary<string, WindowAlertState>(StringComparer.OrdinalIgnoreCase);
            settings.AlertStates = new Dictionary<string, WindowAlertState>(settings.AlertStates, StringComparer.OrdinalIgnoreCase);
            foreach (var key in settings.AlertStates.Keys.ToArray())
            {
                var state = settings.AlertStates[key];
                if (state.Level > 2) settings.AlertStates[key] = state with { Level = state.Level >= 95 ? 2 : 1 };
            }
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
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var temporary = _filePath + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, _filePath, overwrite: true);
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
