using Avalonia;
using Avalonia.Controls;

namespace CodexMonitor.Desktop;

// The only tray menu implementation. Its content is shared across all hosts.
internal sealed class DesktopTrayShell(
    IStartupService startup, MonitorSettings settings, UsageCoordinator coordinator) : IDisposable
{
    private PluginInfo _plugin = coordinator.PluginInfo;
    private TrayIcon? _icon;
    private readonly NativeMenu _windowMenu = new();
    private readonly NativeMenuItem _refreshTokensItem = new("Refresh tokens");
    private readonly NativeMenuItem _alertsItem = new();
    private readonly NativeMenuItem _startupItem = new();
    private readonly NativeMenuItem _repairItem = new();
    private readonly NativeMenuItem _snoozeItem = new();

    public event Action<string>? ErrorOccurred;
    public event Action? Changed;

    public void Install(Action show, Action agents, Action settingsAction, Action quit)
    {
        var menu = new NativeMenu();
        Add(menu, "Show usage", show);
        Add(menu, "Refresh limits", () => _ = coordinator.RefreshLimitsAsync());
        _refreshTokensItem.Click += (_, _) => _ = coordinator.RefreshTokensAsync(force: true);
        menu.Add(_refreshTokensItem);
        menu.Add(new NativeMenuItemSeparator());

        menu.Add(new NativeMenuItem("Tray limit") { Menu = _windowMenu });
        RebuildLimitChoices();

        Add(menu, "AI agents...", agents);
        Add(menu, "Settings...", settingsAction);

        _alertsItem.Click += (_, _) =>
        {
            settings.AlertsEnabled = !settings.AlertsEnabled;
            SaveSettings();
            Update(coordinator.Dashboard);
        };
        menu.Add(_alertsItem);

        _snoozeItem.Click += (_, _) =>
        {
            coordinator.ToggleSnooze();
            SaveSettings();
            Update(coordinator.Dashboard);
        };
        menu.Add(_snoozeItem);

        _repairItem.Click += (_, _) => _ = coordinator.RepairAsync();
        menu.Add(_repairItem);

        _startupItem.Click += (_, _) =>
        {
            try { startup.Enabled = !startup.Enabled; }
            catch (Exception ex) { ErrorOccurred?.Invoke(ex.Message); }
            Update(coordinator.Dashboard);
        };
        menu.Add(_startupItem);
        menu.Add(new NativeMenuItemSeparator());
        Add(menu, "Exit", quit);

        menu.NeedsUpdate += (_, _) => Update(coordinator.Dashboard);
        var resourceName = OperatingSystem.IsWindows()
            ? "CodexMonitor.Tray.ico" : "CodexMonitor.Tray.png";
        using var image = typeof(DesktopTrayShell).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("Embedded tray icon is missing: " + resourceName);
        _icon = new TrayIcon
        {
            Icon = new WindowIcon(image),
            ToolTipText = "Codex Monitor",
            Menu = menu,
            IsVisible = true
        };
        _icon.Clicked += (_, _) => show();
        TrayIcon.SetIcons(Application.Current
            ?? throw new InvalidOperationException("Avalonia application is not initialized."),
            new TrayIcons { _icon });
        Update(coordinator.Dashboard);
    }

    private static void Add(NativeMenu menu, string title, Action callback)
    {
        var item = new NativeMenuItem(title);
        item.Click += (_, _) => callback();
        menu.Add(item);
    }

    private void RebuildLimitChoices()
    {
        _windowMenu.Items.Clear();
        Add(_windowMenu, "Automatic", () => SelectLimit("auto"));
        foreach (var choice in _plugin.WindowOptions)
        {
            var key = choice.Key;
            Add(_windowMenu, choice.Label, () => SelectLimit(key));
        }
    }

    private void SelectLimit(string key)
    {
        coordinator.SetTrayWindow(key);
        SaveSettings();
        Changed?.Invoke();
        Update(coordinator.Dashboard);
    }

    public void SetPlugin(PluginInfo plugin)
    {
        _plugin = plugin;
        RebuildLimitChoices();
        Update(coordinator.Dashboard);
    }

    public void Update(DashboardState state)
    {
        if (_icon is null) return;
        var selected = state.SelectedLimit;
        var tooltip = selected is null ? $"{_plugin.DisplayName}: no data"
            : $"{_plugin.DisplayName}: {selected.UsedPercent:0}% used · {UsageDisplayText.WindowName(selected.Name)}";
        _icon.ToolTipText = tooltip.Length > 110 ? tooltip[..110] : tooltip;
        _refreshTokensItem.IsVisible = _plugin.SupportsTokens;
        _repairItem.IsVisible = _plugin.CanRepair && state.DependencyProblem != DependencyProblem.None;
        _repairItem.Header = state.DependencyProblem == DependencyProblem.Broken
            ? "Repair CLI" : "Install CLI";
        _alertsItem.Header = settings.AlertsEnabled ? "Disable usage alerts" : "Enable usage alerts";
        _startupItem.IsEnabled = startup.CanConfigure;
        _startupItem.Header = startup.Enabled ? "Disable login startup" : "Enable login startup";
        _snoozeItem.IsEnabled = settings.AlertsEnabled && selected?.ResetsAt is not null;
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
