using CodexMonitor.Infrastructure.Windows;

namespace CodexMonitor;

// WinForms host only: it wires timers, tray commands and the view to the
// provider-agnostic UsageCoordinator. Data access and alert policy live there.
internal sealed class MonitorContext : ApplicationContext
{
    private readonly IUsagePlugin _plugin;
    private AgentCatalogSnapshot _agentCatalog;
    private readonly AgentSelection _selection;
    private readonly MonitorSettings _settings;
    private readonly CancellationTokenSource _shutdown;
    private readonly IAgentDiscovery _discovery;
    private readonly IPluginCatalog _plugins;
    private readonly IStartupService _startup;
    private readonly UsageCoordinator _coordinator;
    private readonly IDashboardView _popup;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _displayTimer;
    private DateTimeOffset _lastAgentScanAt;
    private readonly MonitorTrayMenu _menu;
    private Icon? _icon;

    public MonitorContext(AgentSelection selection, AgentCatalogSnapshot agentCatalog,
        MonitorSettings settings, UsageCoordinator coordinator, IAgentDiscovery discovery,
        IPluginCatalog plugins, IStartupService startup, CancellationTokenSource shutdown,
        IDashboardView popup)
    {
        _selection = selection;
        _agentCatalog = agentCatalog;
        _plugin = selection.Plugin;
        _settings = settings;
        _coordinator = coordinator;
        _discovery = discovery;
        _plugins = plugins;
        _startup = startup;
        _shutdown = shutdown;
        _coordinator.Changed += UpdateView;

        _popup = popup;
        _popup.RefreshRequested += (_, _) => _ = _coordinator.RefreshLimitsAsync();
        _popup.InstallRequested += (_, _) => _ = _coordinator.RepairAsync();
        _popup.SettingsRequested += (_, _) => ConfigureSettings();
        _popup.TokensRequested += (_, _) => _ = _coordinator.RefreshTokensAsync(force: false);
        _popup.TokenRefreshRequested += (_, _) => _ = _coordinator.RefreshTokensAsync(force: true);

        _menu = new MonitorTrayMenu(_plugin.Info, _settings, _coordinator, _startup);
        _menu.ShowRequested += (_, _) => ShowPopup();
        _menu.AgentStatusRequested += (_, _) => ShowAgentStatus();
        _menu.SettingsRequested += (_, _) => ConfigureSettings();
        _menu.ExitRequested += (_, _) => ExitThread();
        _menu.RedrawRequested += (_, _) => UpdateView();
        _tray = new NotifyIcon { ContextMenuStrip = _menu.Menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };
        _coordinator.AlertRaised += (title, message, severity) => _tray.ShowBalloonTip(4000,
            title, message, severity == UsageAlertSeverity.Info ? ToolTipIcon.Info : ToolTipIcon.Warning);

        _lastAgentScanAt = DateTimeOffset.UtcNow;
        _refreshTimer = new System.Windows.Forms.Timer { Interval = MinutesToMilliseconds(_settings.LimitsRefreshMinutes) };
        _refreshTimer.Tick += (_, _) => _ = _coordinator.RefreshLimitsAsync();
        _refreshTimer.Start();
        _displayTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _displayTimer.Tick += (_, _) =>
        {
            UpdateView();
            if (_settings.AgentScanMinutes > 0 && DateTimeOffset.UtcNow - _lastAgentScanAt >= TimeSpan.FromMinutes(_settings.AgentScanMinutes))
                RefreshAgentCatalog();
            if (_settings.AutoRefreshTokens && _popup.IsOpen && _popup.ShowingTokens)
                _ = _coordinator.RefreshTokensAsync(force: false);
        };
        _displayTimer.Start();
        UpdateView();
        Application.Idle += StartFirstRefresh;
    }

    private void StartFirstRefresh(object? sender, EventArgs e)
    {
        Application.Idle -= StartFirstRefresh;
        _ = _coordinator.RefreshLimitsAsync();
    }

    private void ShowAgentStatus()
    {
        RefreshAgentCatalog();
        var selection = AgentCatalog.Select(_agentCatalog, _plugins.All, _settings.PreferredAgentId);
        using var dialog = new AgentStatusDialog(_agentCatalog, selection);
        dialog.ShowDialog();
    }

    private void RefreshAgentCatalog()
    {
        _agentCatalog = _discovery.Scan();
        _lastAgentScanAt = DateTimeOffset.UtcNow;
    }

    private void ConfigureSettings()
    {
        using var dialog = new AlertSettingsDialog(_settings.WarningThreshold, _settings.CriticalThreshold,
            _settings.LimitsRefreshMinutes, _settings.TokenRefreshMinutes, _settings.AgentScanMinutes,
            _settings.AutoRefreshTokens, _settings.AlertsEnabled,
            _startup.CanConfigure && _startup.Enabled, _startup.CanConfigure);
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _settings.WarningThreshold = dialog.WarningThreshold;
        _settings.CriticalThreshold = dialog.CriticalThreshold;
        _settings.LimitsRefreshMinutes = dialog.LimitsRefreshMinutes;
        _settings.TokenRefreshMinutes = dialog.TokenRefreshMinutes;
        _settings.AgentScanMinutes = dialog.AgentScanMinutes;
        _settings.AutoRefreshTokens = dialog.AutoRefreshTokens;
        _settings.AlertsEnabled = dialog.AlertsEnabled;
        _refreshTimer.Interval = MinutesToMilliseconds(_settings.LimitsRefreshMinutes);
        _menu.SyncSettings(dialog.StartWithWindows);
        SaveSettings();
        if (_coordinator.ShowingFreshLimits) _ = _coordinator.RefreshLimitsAsync();
    }

    private void SaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Codex Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ShowPopup()
    {
        UpdatePopup();
        _popup.ShowNearCursor();
        if (_popup.ShowingTokens) _ = _coordinator.RefreshTokensAsync(force: false);
    }

    private void UpdateView()
    {
        if (_shutdown.IsCancellationRequested) return;
        var main = _coordinator.SelectedWindow;
        var oldIcon = _icon;
        _icon = TrayIcon.Create(main?.UsedPercent, _coordinator.LimitError is not null);
        _tray.Icon = _icon;
        oldIcon?.Dispose();
        var summary = main is null ? $"{_plugin.Info.DisplayName}: no data" :
            $"{main.Name}: {main.UsedPercent:0}% used, {UsageDisplayText.ResetShort(main.ResetsAt)}";
        _tray.Text = summary.Length > 63 ? summary[..63] : summary;
        _menu.UpdateStatus();
        UpdatePopup();
    }

    private void UpdatePopup() => _popup.ShowSnapshot(_coordinator.Dashboard);

    private static int MinutesToMilliseconds(int minutes) =>
        (int)TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 60)).TotalMilliseconds;

    protected override void ExitThreadCore()
    {
        _shutdown.Cancel();
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _displayTimer.Stop();
        _displayTimer.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _menu.Dispose();
        _icon?.Dispose();
        // The DI container owns the dashboard view's lifetime.
        base.ExitThreadCore();
    }
}
