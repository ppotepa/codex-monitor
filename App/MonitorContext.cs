using CodexMonitor.Infrastructure.Windows;

namespace CodexMonitor;

// WinForms host only: it wires timers, tray commands and the view to the
// provider-agnostic UsageCoordinator. Data access and alert policy live there.
internal sealed class MonitorContext : ApplicationContext
{
    private readonly IUsagePlugin _plugin;
    private AgentCatalogSnapshot _agentCatalog;
    private readonly AgentSelection _selection;
    private readonly MonitorSettings _settings = MonitorSettings.Load();
    private readonly UsageHistory _history = UsageHistory.Load();
    private readonly MonitorDatabase _database;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly UsageCoordinator _coordinator;
    private readonly UsagePopup _popup;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly System.Windows.Forms.Timer _displayTimer;
    private DateTimeOffset _lastAgentScanAt;
    private ToolStripMenuItem _installItem = null!;
    private ToolStripMenuItem _startupItem = null!;
    private ToolStripMenuItem[] _windowOptions = null!;
    private ToolStripMenuItem _alertsItem = null!;
    private ToolStripMenuItem _snoozeItem = null!;
    private Icon? _icon;

    public MonitorContext(AgentSelection selection, AgentCatalogSnapshot agentCatalog)
    {
        _selection = selection;
        _agentCatalog = agentCatalog;
        _plugin = selection.Plugin;
        _database = _history.Database ?? MonitorDatabase.Load();
        _coordinator = new UsageCoordinator(_plugin, _settings, _history, _database, _shutdown.Token);
        _coordinator.Changed += UpdateView;

        _popup = new UsagePopup(_plugin.Info);
        _popup.RefreshRequested += (_, _) => _ = _coordinator.RefreshLimitsAsync();
        _popup.InstallRequested += (_, _) => _ = _coordinator.RepairAsync();
        _popup.SettingsRequested += (_, _) => ConfigureSettings();
        _popup.TokensRequested += (_, _) => _ = _coordinator.RefreshTokensAsync(force: false);
        _popup.TokenRefreshRequested += (_, _) => _ = _coordinator.RefreshTokensAsync(force: true);

        var menu = BuildTrayMenu();
        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };
        _coordinator.AlertRaised += (title, message, icon) => _tray.ShowBalloonTip(4000, title, message, icon);

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
            if (_settings.AutoRefreshTokens && _popup.Visible && _popup.ShowingTokens)
                _ = _coordinator.RefreshTokensAsync(force: false);
        };
        _displayTimer.Start();
        UpdateView();
        Application.Idle += StartFirstRefresh;
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show usage", null, (_, _) => ShowPopup());
        menu.Items.Add("Refresh", null, (_, _) => _ = _coordinator.RefreshLimitsAsync());
        _installItem = new ToolStripMenuItem("Install usage CLI", null, (_, _) => _ = _coordinator.RepairAsync());
        menu.Items.Add(_installItem);

        var displayMenu = new ToolStripMenuItem("Tray icon shows");
        var windowChoices = new[] { new UsageWindowOption("auto", "Automatic") }.Concat(_plugin.Info.WindowOptions);
        _windowOptions = windowChoices.Select(choice =>
        {
            var item = new ToolStripMenuItem(choice.Label) { Tag = choice.Key };
            item.Click += (_, _) =>
            {
                _coordinator.SetTrayWindow(choice.Key);
                UpdateWindowChecks();
                SaveSettings();
                UpdateView();
            };
            displayMenu.DropDownItems.Add(item);
            return item;
        }).ToArray();
        UpdateWindowChecks();
        menu.Items.Add(displayMenu);

        menu.Items.Add("AI agents...", null, (_, _) => ShowAgentStatus());

        _alertsItem = new ToolStripMenuItem("Usage alerts") { Checked = _settings.AlertsEnabled, CheckOnClick = true };
        _alertsItem.CheckedChanged += (_, _) => { _settings.AlertsEnabled = _alertsItem.Checked; SaveSettings(); };
        menu.Items.Add(_alertsItem);
        menu.Items.Add("Settings...", null, (_, _) => ConfigureSettings());
        _snoozeItem = new ToolStripMenuItem("Snooze alerts until reset", null, (_, _) => ToggleSnooze());
        menu.Items.Add(_snoozeItem);
        menu.Opening += (_, _) => UpdateSnoozeItem();

        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleAutoStart());
        _startupItem.Enabled = AutoStart.IsPackagedExecutable;
        _startupItem.Checked = AutoStart.IsPackagedExecutable && AutoStart.Enabled;
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void StartFirstRefresh(object? sender, EventArgs e)
    {
        Application.Idle -= StartFirstRefresh;
        _ = _coordinator.RefreshLimitsAsync();
    }

    private void ShowAgentStatus()
    {
        RefreshAgentCatalog();
        var selection = AgentCatalog.Select(_agentCatalog, PluginRegistry.All, _settings.PreferredAgentId);
        using var dialog = new AgentStatusDialog(_agentCatalog, selection);
        dialog.ShowDialog();
    }

    private void RefreshAgentCatalog()
    {
        _agentCatalog = AgentDiscovery.Scan();
        _lastAgentScanAt = DateTimeOffset.UtcNow;
    }

    private void ConfigureSettings()
    {
        using var dialog = new AlertSettingsDialog(_settings.WarningThreshold, _settings.CriticalThreshold,
            _settings.LimitsRefreshMinutes, _settings.TokenRefreshMinutes, _settings.AgentScanMinutes,
            _settings.AutoRefreshTokens, _settings.AlertsEnabled,
            AutoStart.IsPackagedExecutable && AutoStart.Enabled, AutoStart.IsPackagedExecutable);
        if (dialog.ShowDialog() != DialogResult.OK) return;
        _settings.WarningThreshold = dialog.WarningThreshold;
        _settings.CriticalThreshold = dialog.CriticalThreshold;
        _settings.LimitsRefreshMinutes = dialog.LimitsRefreshMinutes;
        _settings.TokenRefreshMinutes = dialog.TokenRefreshMinutes;
        _settings.AgentScanMinutes = dialog.AgentScanMinutes;
        _settings.AutoRefreshTokens = dialog.AutoRefreshTokens;
        _settings.AlertsEnabled = dialog.AlertsEnabled;
        _refreshTimer.Interval = MinutesToMilliseconds(_settings.LimitsRefreshMinutes);
        _alertsItem.Checked = dialog.AlertsEnabled;
        if (AutoStart.IsPackagedExecutable && AutoStart.Enabled != dialog.StartWithWindows)
        {
            try
            {
                AutoStart.Enabled = dialog.StartWithWindows;
                _startupItem.Checked = dialog.StartWithWindows;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Codex Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        _coordinator.ResetAlertLevels();
        SaveSettings();
        if (_coordinator.ShowingFreshLimits) _ = _coordinator.RefreshLimitsAsync();
    }

    private void ToggleAutoStart()
    {
        try
        {
            AutoStart.Enabled = !_startupItem.Checked;
            _startupItem.Checked = AutoStart.Enabled;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Codex Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ToggleSnooze()
    {
        _coordinator.ToggleSnooze();
        SaveSettings();
        UpdateSnoozeItem();
    }

    private void UpdateSnoozeItem()
    {
        var selected = _coordinator.SelectedWindow;
        _snoozeItem.Enabled = _settings.AlertsEnabled && selected?.ResetsAt is not null;
        _snoozeItem.Checked = _coordinator.IsSnoozed();
    }

    private void UpdateWindowChecks()
    {
        foreach (var item in _windowOptions)
            item.Checked = string.Equals(item.Tag as string, _settings.TrayWindow, StringComparison.OrdinalIgnoreCase);
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
            $"{main.Name}: {main.UsedPercent:0}% used, {UsagePopup.ResetShort(main.ResetsAt)}";
        _tray.Text = summary.Length > 63 ? summary[..63] : summary;
        _installItem.Visible = _plugin.Info.CanRepair && _coordinator.DependencyProblem != DependencyProblem.None;
        _installItem.Text = _coordinator.DependencyProblem == DependencyProblem.Broken ? "Repair usage CLI" : "Install usage CLI";
        UpdatePopup();
    }

    private void UpdatePopup() => _popup.ShowSnapshot(_coordinator.Snapshot, _coordinator.SelectedWindow,
        _coordinator.RecentHistory, _coordinator.LimitError, _coordinator.IsBusy,
        _coordinator.DependencyProblem, _coordinator.TokenUsage, _coordinator.TokenError,
        _coordinator.IsTokenBusy, _coordinator.PluginStatus);

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
        _icon?.Dispose();
        _popup.Dispose();
        _coordinator.Dispose();
        _shutdown.Dispose();
        base.ExitThreadCore();
    }
}
