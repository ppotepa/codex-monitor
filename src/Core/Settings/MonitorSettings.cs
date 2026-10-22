namespace CodexMonitor.Core.Settings;

// In-memory preferences. Serialization, recovery and file ownership belong to Infrastructure.
public sealed class MonitorSettings
{
    public string TrayWindow { get; set; } = "auto";
    public string PreferredAgentId { get; set; } = "auto";
    public string WorkspaceScope { get; set; } = "Global";
    public string WorkspaceAgentId { get; set; } = "codex";
    public string? WorkspaceProjectPath { get; set; }
    public List<string> ProjectPaths { get; set; } = [];
    public bool AlertsEnabled { get; set; } = true;
    public int WarningThreshold { get; set; } = 80;
    public int CriticalThreshold { get; set; } = 95;
    public int LimitsRefreshMinutes { get; set; } = 5;
    public int TokenRefreshMinutes { get; set; } = 30;
    public int AgentScanMinutes { get; set; } = 30;
    public bool AutoRefreshTokens { get; set; } = true;
    public Dictionary<string, WindowAlertState> AlertStates { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed record WindowAlertState(long? ResetAt, int Level, bool Snoozed = false);
