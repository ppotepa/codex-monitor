namespace CodexMonitor;

// Owns Windows tray commands and their UI state. The host owns lifetime and dialogs.
internal sealed class MonitorTrayMenu : IDisposable
{
    private readonly PluginInfo _plugin;
    private readonly MonitorSettings _settings;
    private readonly UsageCoordinator _coordinator;
    private readonly IStartupService _startup;
    private readonly ToolStripMenuItem _installItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly ToolStripMenuItem _alertsItem;
    private readonly ToolStripMenuItem _snoozeItem;
    private readonly ToolStripMenuItem[] _windowOptions;

    public ContextMenuStrip Menu { get; } = new();

    public event EventHandler? ShowRequested;
    public event EventHandler? AgentStatusRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler? RedrawRequested;

    public MonitorTrayMenu(PluginInfo plugin, MonitorSettings settings,
        UsageCoordinator coordinator, IStartupService startup)
    {
        _plugin = plugin;
        _settings = settings;
        _coordinator = coordinator;
        _startup = startup;

        Menu.Items.Add("Show usage", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        Menu.Items.Add("Refresh", null, (_, _) => _ = _coordinator.RefreshLimitsAsync());
        _installItem = new ToolStripMenuItem("Install usage CLI", null, (_, _) => _ = _coordinator.RepairAsync());
        Menu.Items.Add(_installItem);

        var displayMenu = new ToolStripMenuItem("Tray icon shows");
        var choices = new[] { new UsageWindowOption("auto", "Automatic") }.Concat(_plugin.WindowOptions);
        _windowOptions = choices.Select(choice =>
        {
            var item = new ToolStripMenuItem(choice.Label) { Tag = choice.Key };
            item.Click += (_, _) =>
            {
                _coordinator.SetTrayWindow(choice.Key);
                UpdateWindowChecks();
                SaveSettings();
                RedrawRequested?.Invoke(this, EventArgs.Empty);
            };
            displayMenu.DropDownItems.Add(item);
            return item;
        }).ToArray();
        UpdateWindowChecks();
        Menu.Items.Add(displayMenu);

        Menu.Items.Add("AI agents...", null, (_, _) => AgentStatusRequested?.Invoke(this, EventArgs.Empty));
        _alertsItem = new ToolStripMenuItem("Usage alerts") { Checked = _settings.AlertsEnabled, CheckOnClick = true };
        _alertsItem.CheckedChanged += (_, _) => { _settings.AlertsEnabled = _alertsItem.Checked; SaveSettings(); };
        Menu.Items.Add(_alertsItem);
        Menu.Items.Add("Settings...", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));

        _snoozeItem = new ToolStripMenuItem("Snooze alerts until reset", null, (_, _) => ToggleSnooze());
        Menu.Items.Add(_snoozeItem);
        Menu.Opening += (_, _) => UpdateSnoozeItem();

        _startupItem = new ToolStripMenuItem(_startup.MenuLabel, null, (_, _) => ToggleAutoStart());
        _startupItem.Enabled = _startup.CanConfigure;
        _startupItem.Checked = _startup.CanConfigure && _startup.Enabled;
        Menu.Items.Add(_startupItem);
        Menu.Items.Add(new ToolStripSeparator());
        Menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public void UpdateStatus()
    {
        _installItem.Visible = _plugin.CanRepair && _coordinator.DependencyProblem != DependencyProblem.None;
        _installItem.Text = _coordinator.DependencyProblem == DependencyProblem.Broken
            ? "Repair usage CLI" : "Install usage CLI";
    }

    public void SyncSettings(bool startAtLogin)
    {
        _alertsItem.Checked = _settings.AlertsEnabled;
        if (_startup.CanConfigure && _startup.Enabled != startAtLogin)
        {
            try
            {
                _startup.Enabled = startAtLogin;
                _startupItem.Checked = _startup.Enabled;
            }
            catch (Exception ex) { ShowStartupError(ex); }
        }
        UpdateSnoozeItem();
    }

    private void ToggleAutoStart()
    {
        try
        {
            _startup.Enabled = !_startupItem.Checked;
            _startupItem.Checked = _startup.Enabled;
        }
        catch (Exception ex) { ShowStartupError(ex); }
    }

    private static void ShowStartupError(Exception ex) =>
        MessageBox.Show(ex.Message, "Codex Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);

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

    public void Dispose() => Menu.Dispose();
}
