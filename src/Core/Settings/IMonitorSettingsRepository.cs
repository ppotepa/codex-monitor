namespace CodexMonitor.Core.Settings;

public interface IMonitorSettingsRepository
{
    string? PersistenceError { get; }
    MonitorSettings Load();
    void Save(MonitorSettings settings);
}
