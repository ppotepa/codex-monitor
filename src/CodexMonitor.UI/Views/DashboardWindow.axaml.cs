using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

// Single Avalonia window and AXAML layout for Windows, Linux and macOS.
public partial class DashboardWindow : Window
{
    private readonly DashboardViewModel _model;
    private readonly bool _hideOnClose;
    private bool _trayAvailable;
    private bool _exit;

    internal DashboardWindow(DashboardViewModel model, bool hideOnClose)
    {
        _model = model;
        _hideOnClose = hideOnClose;
        AvaloniaXamlLoader.Load(this);
        DataContext = model;

        model.RefreshLimitsRequested += () => RefreshRequested?.Invoke(this, EventArgs.Empty);
        model.RefreshTokensRequested += () => TokenRefreshRequested?.Invoke(this, EventArgs.Empty);
        model.RepairRequested += () => InstallRequested?.Invoke(this, EventArgs.Empty);
        model.SettingsRequested += () => SettingsRequested?.Invoke(this, EventArgs.Empty);

        var tabs = this.FindControl<TabControl>("MainTabs")
            ?? throw new InvalidOperationException("Dashboard AXAML is missing MainTabs.");
        tabs.SelectionChanged += (_, _) =>
        {
            if (ShowingTokens) TokensRequested?.Invoke(this, EventArgs.Empty);
        };
        Closing += (_, eventArgs) =>
        {
            if (_exit || !_trayAvailable || !_hideOnClose) return;
            eventArgs.Cancel = true;
            Hide();
        };
    }

    public event EventHandler? RefreshRequested;
    public event EventHandler? InstallRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TokensRequested;
    public event EventHandler? TokenRefreshRequested;

    public bool IsOpen => IsVisible;
    public bool ShowingTokens =>
        _model.SupportsTokens && this.FindControl<TabControl>("MainTabs")?.SelectedIndex == 1;

    internal void SetPlugin(PluginInfo plugin)
    {
        var tabs = this.FindControl<TabControl>("MainTabs")
            ?? throw new InvalidOperationException("Dashboard tabs are unavailable.");
        var tokenTabWasSelected = tabs.SelectedIndex == 1;
        _model.SetPlugin(plugin);
        if (!plugin.SupportsTokens && tokenTabWasSelected)
            tabs.SelectedIndex = 0;
    }

    internal void ApplyState(DashboardState state) => _model.Apply(state, _currentPlugin
        ?? throw new InvalidOperationException("Select a plugin before displaying its usage."));
    private PluginInfo? _currentPlugin;

    internal void SelectPlugin(PluginInfo plugin)
    {
        _currentPlugin = plugin;
        SetPlugin(plugin);
    }

    internal void SetTrayAvailable(bool available)
    {
        _trayAvailable = available;
        ShowInTaskbar = !available || !_hideOnClose;
    }

    internal void SetMessage(string message) => _model.SetNotice(message);

    public void ShowNearCursor()
    {
        // Window positioning on Wayland is controlled by the compositor.
        if (!IsVisible) Show();
        Activate();
    }

    public void Dispose()
    {
        _exit = true;
        Close();
    }
}
