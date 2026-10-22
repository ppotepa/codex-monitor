using System.Text.Json;

namespace CodexMonitor.Infrastructure.Persistence;

internal sealed class JsonMonitorSettingsRepository(string filePath) : IMonitorSettingsRepository
{
    private bool _saveBlocked;

    public string? PersistenceError { get; private set; }

    public MonitorSettings Load()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            var settings = JsonSerializer.Deserialize<MonitorSettings>(File.ReadAllText(filePath))
                ?? throw new JsonException("Settings JSON is empty.");
            ValidateAndNormalize(settings);
            return settings;
        }
        catch (FileNotFoundException) { return new MonitorSettings(); }
        catch (DirectoryNotFoundException) { return new MonitorSettings(); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return RecoverUnreadable(ex);
        }
    }

    private MonitorSettings RecoverUnreadable(Exception error)
    {
        var settings = new MonitorSettings();
        var backup = $"{filePath}.corrupt.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        try
        {
            File.Copy(filePath, backup, overwrite: false);
            PersistenceError =
                $"Settings could not be loaded ({error.Message}); the original was preserved as {Path.GetFileName(backup)}. Defaults are being used.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _saveBlocked = true;
            PersistenceError =
                $"Settings could not be loaded ({error.Message}) or preserved ({ex.Message}). Saving is blocked to protect the original file.";
        }
        return settings;
    }

    private static void ValidateAndNormalize(MonitorSettings settings)
    {
        if (settings.AlertStates is null ||
            settings.AlertStates.Any(pair =>
                pair.Key.IndexOf(':') <= 0 || pair.Key.EndsWith(':') ||
                pair.Value is null || pair.Value.Level is < 0 or > 2))
            throw new JsonException("Invalid settings format: expected provider-scoped keys and levels 0-2.");

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
    }

    public void Save(MonitorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_saveBlocked) throw new IOException(
            "Cannot save settings because the unreadable original could not be preserved.");

        var temporary = filePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, filePath, overwrite: true);
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
