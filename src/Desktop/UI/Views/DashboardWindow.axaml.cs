using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CodexMonitor.UI.ViewModels;

namespace CodexMonitor.UI.Views;

// Single Avalonia window and AXAML layout for Windows, Linux and macOS.
public partial class DashboardWindow : Window
{
    private readonly DashboardViewModel _model;
    private readonly bool _hideOnClose;
    private bool _trayAvailable;
    private bool _exit;

    // Required by Avalonia's AXAML compiler/previewer; the desktop host uses
    // the model-aware overload below.
    public DashboardWindow() : this(new DashboardViewModel(), hideOnClose: false) { }

    internal DashboardWindow(DashboardViewModel model, bool hideOnClose)
    {
        _model = model;
        _hideOnClose = hideOnClose;
        AvaloniaXamlLoader.Load(this);
        DataContext = model;
        KeyDown += (_, e) =>
        {
            if (e.Key != Avalonia.Input.Key.Escape) return;
            Close();
            e.Handled = true;
        };
        this.FindControl<Grid>("WindowHeader")!.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            // Header decorations and empty space are draggable, but button
            // descendants must keep their normal pointer interaction.
            if (e.Source is Avalonia.Visual source &&
                (source is Button || source.GetVisualAncestors().Any(parent => parent is Button))) return;
            BeginMoveDrag(e);
            e.Handled = true;
        };

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
    private void CloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();
    public event EventHandler? InstallRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TokensRequested;
    public event EventHandler? TokenRefreshRequested;

    public bool IsOpen => IsVisible;
    public bool ShowingTokens =>
        _model.SupportsTokens && this.FindControl<TabControl>("MainTabs")?.SelectedIndex == 1;

    internal void SetProvider(ProviderInfo provider)
    {
        var tabs = this.FindControl<TabControl>("MainTabs")
            ?? throw new InvalidOperationException("Dashboard tabs are unavailable.");
        var tokenTabWasSelected = tabs.SelectedIndex == 1;
        _model.SetProvider(provider);
        if (!provider.SupportsTokens && tokenTabWasSelected)
            tabs.SelectedIndex = 0;
    }

    internal void ApplyState(DashboardState state) => _model.Apply(state, _currentProvider
        ?? throw new InvalidOperationException("Select a provider before displaying its usage."));
    private ProviderInfo? _currentProvider;

    internal void SelectProvider(ProviderInfo provider)
    {
        _currentProvider = provider;
        SetProvider(provider);
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
