using Avalonia;
using Avalonia.Controls;

namespace CodexMonitor.Desktop.Unix;

// Tray platform quirks remain inside the Avalonia host. The menu always has
// "Show" because macOS does not dispatch the tray icon Clicked event.
internal sealed class UnixTrayShell(IStartupService startup, MonitorSettings settings,
    UsageCoordinator coordinator, IUsagePlugin plugin) : IDisposable
{
    private PluginInfo _plugin = plugin.Info;
    private TrayIcon? _icon;
    private readonly NativeMenuItem _startupItem = new();
    private readonly NativeMenuItem _repairItem = new();
    private readonly NativeMenuItem _snoozeItem = new();

    public void Install(Action show, Action agents, Action settingsAction, Action quit)
    {
        var menu = new NativeMenu();
        Add(menu, "Show usage", show);
        Add(menu, "Refresh limits", () => _ = coordinator.RefreshLimitsAsync());
        if (_plugin.SupportsTokens)
            Add(menu, "Refresh tokens", () => _ = coordinator.RefreshTokensAsync(force: true));
        menu.Add(new NativeMenuItemSeparator());

        var windows = new NativeMenu();
        Add(windows, "Automatic", () => SetWindow("auto"));
        foreach (var option in _plugin.WindowOptions)
        {
            var key = option.Key;
            Add(windows, option.Label, () => SetWindow(key));
        }
        menu.Add(new NativeMenuItem("Tray limit") { Menu = windows });

        Add(menu, "AI agents...", agents);
        Add(menu, "Settings...", settingsAction);

        _snoozeItem.Click += (_, _) =>
        {
            coordinator.ToggleSnooze();
            SaveSettings();
            Update(coordinator.Dashboard);
        };
        menu.Add(_snoozeItem);

        _repairItem.Header = "Repair CLI";
        _repairItem.Click += (_, _) => _ = coordinator.RepairAsync();
        menu.Add(_repairItem);

        _startupItem.Click += (_, _) =>
        {
            try
            {
                startup.Enabled = !startup.Enabled;
                Update(coordinator.Dashboard);
            }
            catch (Exception ex)
            {
                // Window-level status stays available even on desktops without
                // notification-daemon integration.
                ErrorOccurred?.Invoke(ex.Message);
            }
        };
        menu.Add(_startupItem);
        menu.Add(new NativeMenuItemSeparator());
        Add(menu, "Exit", quit);

        using var image = typeof(UnixTrayShell).Assembly.GetManifestResourceStream("CodexMonitor.Tray.png")
            ?? throw new InvalidOperationException("Embedded tray icon is missing.");
        _icon = new TrayIcon
        {
            Icon = new WindowIcon(image),
            ToolTipText = "Codex Monitor",
            Menu = menu,
            IsVisible = true
        };
        _icon.Clicked += (_, _) => show();
        TrayIcon.SetIcons(Application.Current!, new TrayIcons { _icon });
        Update(coordinator.Dashboard);
    }

    public event Action<string>? ErrorOccurred;

    private static void Add(NativeMenu menu, string label, Action action)
    {
        var item = new NativeMenuItem(label);
        item.Click += (_, _) => action();
        menu.Add(item);
    }

    private void SetWindow(string key)
    {
        coordinator.SetTrayWindow(key);
        SaveSettings();
        Update(coordinator.Dashboard);
        Changed?.Invoke();
    }

    public event Action? Changed;

    public void SetPlugin(PluginInfo pluginInfo)
    {
        _plugin = pluginInfo;
        Update(coordinator.Dashboard);
    }

    public void Update(DashboardState state)
    {
        if (_icon is null) return;
        var window = state.SelectedLimit;
        _icon.ToolTipText = window is null ? "Codex Monitor: no usage data"
            : $"{_plugin.DisplayName}: {window.UsedPercent:0}% used · {UsageDisplayText.WindowName(window.Name)}";
        _repairItem.IsVisible = _plugin.CanRepair &&
            state.DependencyProblem != DependencyProblem.None;
        _repairItem.Header = state.DependencyProblem == DependencyProblem.Broken
            ? "Repair CLI" : "Install CLI";
        _startupItem.IsEnabled = startup.CanConfigure;
        _startupItem.Header = startup.Enabled ? "Disable login startup" : "Enable login startup";
        _snoozeItem.IsEnabled = settings.AlertsEnabled && window?.ResetsAt is not null;
        _snoozeItem.Header = coordinator.IsSnoozed() ? "Resume usage alerts" : "Snooze until reset";
    }

    private void SaveSettings()
    {
        try { settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public void Dispose()
    {
        if (_icon is null) return;
        if (Application.Current is { } app) TrayIcon.SetIcons(app, null);
        _icon.Dispose();
        _icon = null;
    }

}
