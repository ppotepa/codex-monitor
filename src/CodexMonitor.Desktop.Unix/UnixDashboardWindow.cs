using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using CodexMonitor.Platform.Unix;

namespace CodexMonitor.Desktop.Unix;

// Same dashboard contract as the Windows host, implemented with Avalonia only.
internal sealed class UnixDashboardWindow : Window, IDashboardView
{
    private readonly PluginInfo _plugin;
    private readonly TextBlock _status = new();
    private readonly TextBlock _notice = new();
    private readonly IWindowClosePolicy _closePolicy;
    private readonly TextBlock _limitTitle = new();
    private readonly TextBlock _remaining = new();
    private readonly TextBlock _reset = new();
    private readonly TextBlock _limitError = new();
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 100, Height = 12 };
    private readonly UsageHistoryChart _history = new();
    private readonly StackPanel _otherWindows = new() { Spacing = 8 };
    private readonly TextBlock _tokenDate = new();
    private readonly TextBlock _latestTokens = new();
    private readonly TextBlock _lifetime = new();
    private readonly TextBlock _peak = new();
    private readonly TextBlock _tokenError = new();
    private readonly StackPanel _dailyTokens = new() { Spacing = 6 };
    private readonly Button _refreshLimits = new() { Content = "Refresh limits" };
    private readonly Button _repair = new() { Content = "Repair CLI" };
    private readonly Button _refreshTokens = new() { Content = "Refresh token activity" };
    private readonly TabControl _tabs = new();
    private bool _closingForExit;
    private bool _trayAvailable;

    public event EventHandler? RefreshRequested;
    public event EventHandler? InstallRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TokensRequested;
    public event EventHandler? TokenRefreshRequested;

    public bool IsOpen => IsVisible;
    public void SetTrayAvailable(bool available) => _trayAvailable = available;
    public bool ShowingTokens => _tabs.SelectedIndex == 1 && _plugin.SupportsTokens;

    public UnixDashboardWindow(PluginInfo plugin, IWindowClosePolicy closePolicy)
    {
        _plugin = plugin;
        _closePolicy = closePolicy;
        Title = "Codex Monitor";
        Width = 440;
        Height = 600;
        MinWidth = 340;
        MinHeight = 420;
        Background = Brush("#161B23");
        Closing += (_, e) =>
        {
            if (_closingForExit || !_trayAvailable || !_closePolicy.HideOnClose) return;
            e.Cancel = true;
            Hide();
        };
        _refreshLimits.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _repair.Click += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
        _refreshTokens.Click += (_, _) => TokenRefreshRequested?.Invoke(this, EventArgs.Empty);

        var title = Label("Codex Monitor", 21, bold: true);
        _status.Foreground = Brush("#9CA9BA");
        _status.Text = "Waiting for usage data";

        var settings = new Button { Content = "Settings" };
        settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        var header = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(settings, Dock.Right);
        header.Children.Add(settings);
        header.Children.Add(title);

        var page = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        page.Children.Add(header);
        page.Children.Add(_status);
        _notice.Foreground = Brush("#F6C160");
        _notice.TextWrapping = TextWrapping.Wrap;
        _notice.IsVisible = false;
        page.Children.Add(_notice);

        _tabs.Items.Add(new TabItem { Header = "Limits", Content = BuildLimits() });
        if (plugin.SupportsTokens)
            _tabs.Items.Add(new TabItem { Header = "Tokens", Content = BuildTokens() });
        _tabs.SelectionChanged += (_, _) =>
        {
            if (ShowingTokens) TokensRequested?.Invoke(this, EventArgs.Empty);
        };
        _tabs.SelectedIndex = 0;
        page.Children.Add(_tabs);

        Content = new ScrollViewer { Content = page };
    }

    private Control BuildLimits()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        _limitTitle.FontWeight = FontWeight.SemiBold;
        _remaining.FontSize = 30;
        _remaining.FontWeight = FontWeight.Bold;
        _reset.Foreground = Brush("#9CA9BA");
        content.Children.Add(Card(new StackPanel
        {
            Spacing = 8,
            Children = { _limitTitle, _remaining, _progress, _reset }
        }));
        content.Children.Add(_otherWindows);
        content.Children.Add(Label("USAGE HISTORY · 24 HOURS", 12, bold: true));
        content.Children.Add(_history);
        _limitError.Foreground = Brush("#F58B8B");
        _limitError.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(_limitError);
        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { _refreshLimits, _repair }
        });
        return content;
    }

    private Control BuildTokens()
    {
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
        _tokenDate.Foreground = Brush("#9CA9BA");
        _latestTokens.FontSize = 30;
        _latestTokens.FontWeight = FontWeight.Bold;
        content.Children.Add(Card(new StackPanel
        {
            Spacing = 5,
            Children = { _tokenDate, _latestTokens, _lifetime, _peak }
        }));
        content.Children.Add(Label("DAILY TOKENS · RECENT DAYS", 12, bold: true));
        content.Children.Add(_dailyTokens);
        _tokenError.Foreground = Brush("#F58B8B");
        _tokenError.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(_tokenError);
        content.Children.Add(_refreshTokens);
        return content;
    }

    public void ShowSnapshot(DashboardState state)
    {
        var selected = state.SelectedLimit;
        _status.Text = state.LimitsBusy ? "Updating limits..." :
            $"{_plugin.DisplayName} · {UsageDisplayText.StatusText(state.PluginStatus.Health)}" +
            (state.Limits?.RetrievedAt is { } at ? $" · {at.ToLocalTime():g}" : "");
        _limitTitle.Text = UsageDisplayText.WindowName(selected?.Name);
        _remaining.Text = selected is null ? "No data" : $"{100 - selected.UsedPercent:0}% left";
        _progress.Value = selected?.UsedPercent ?? 0;
        _history.SetPoints(state.RecentHistory);
        _reset.Text = selected is null ? "Sign in to Codex CLI to see usage."
            : UsageDisplayText.ResetText(selected.ResetsAt);
        _otherWindows.Children.Clear();
        if (state.Limits is not null)
        {
            foreach (var window in state.Limits.Windows.Where(x => !ReferenceEquals(x, selected)))
            {
                _otherWindows.Children.Add(Card(new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        Label($"{UsageDisplayText.WindowName(window.Name)} · {window.UsedPercent:0}% used"),
                        new ProgressBar { Minimum = 0, Maximum = 100, Value = window.UsedPercent, Height = 8 },
                        Label(UsageDisplayText.ResetShort(window.ResetsAt), 11)
                    }
                }));
            }
        }

        _limitError.Text = state.LimitError ?? "";
        _limitError.IsVisible = !string.IsNullOrWhiteSpace(state.LimitError);
        _refreshLimits.IsEnabled = !state.LimitsBusy;
        _repair.IsVisible = _plugin.CanRepair && state.DependencyProblem != DependencyProblem.None;
        _repair.IsEnabled = !state.LimitsBusy;
        _repair.Content = state.DependencyProblem == DependencyProblem.Broken ? "Repair CLI" : "Install CLI";

        var tokens = state.Tokens;
        var latest = tokens?.DailyBuckets.LastOrDefault();
        _tokenDate.Text = latest is null ? "Latest reported day" : latest.Date.ToString("D", CultureInfo.CurrentCulture);
        _latestTokens.Text = latest is null ? "No token data" : UsageDisplayText.FormatTokenCount(latest.Tokens);
        _lifetime.Text = $"Lifetime: {UsageDisplayText.FormatTokenCount(tokens?.LifetimeTokens)}";
        _peak.Text = $"Peak day: {UsageDisplayText.FormatTokenCount(tokens?.PeakDailyTokens)}";
        _dailyTokens.Children.Clear();
        var recent = tokens?.DailyBuckets.TakeLast(7).ToArray() ?? [];
        var max = Math.Max(1L, recent.Select(bucket => bucket.Tokens).DefaultIfEmpty().Max());
        foreach (var bucket in recent)
        {
            _dailyTokens.Children.Add(new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    Label($"{bucket.Date:MMM d} · {bucket.Tokens:N0} tokens", 11),
                    new ProgressBar
                    {
                        Minimum = 0, Maximum = max, Value = Math.Max(0, bucket.Tokens),
                        Height = 10
                    }
                }
            });
        }
        _tokenError.Text = state.TokenError ?? "";
        _tokenError.IsVisible = !string.IsNullOrWhiteSpace(state.TokenError);
        _refreshTokens.IsEnabled = !state.TokensBusy;
    }

    // Used for non-native alerts and platform error messages.
    public void SetMessage(string message)
    {
        _notice.Text = message;
        _notice.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    public void ShowNearCursor()
    {
        // Wayland may prohibit applications from choosing window coordinates.
        if (!IsVisible) Show();
        Activate();
    }

    public void Dispose()
    {
        _closingForExit = true;
        Close();
    }

    private static TextBlock Label(string text, double size = 13, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
        Foreground = Brushes.White,
        TextWrapping = TextWrapping.Wrap
    };

    private static Border Card(Control content) => new()
    {
        Background = Brush("#202732"),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(14),
        Child = content
    };

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
}
