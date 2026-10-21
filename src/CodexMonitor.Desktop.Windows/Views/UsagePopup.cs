using System.Drawing.Drawing2D;
using System.Globalization;

namespace CodexMonitor;

internal sealed class UsagePopup : Form, IDashboardView
{
    private static readonly Color Background = Color.FromArgb(22, 27, 35);
    private static readonly Color Card = Color.FromArgb(32, 39, 50);
    private static readonly Color Muted = Color.FromArgb(155, 168, 185);
    private static readonly Color Accent = Color.FromArgb(83, 207, 192);

    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly PluginInfo _plugin;
    private readonly Label _pluginTag = new();
    private string _limitsSubtitle = "Waiting for usage data";
    private string _tokensSubtitle = "Account token activity";
    private readonly Button _close = new();
    private readonly Button _settings = new();
    private readonly Button _limitsTab = new();
    private readonly Button _tokensTab = new();
    private readonly Panel _limitsPanel = new();
    private readonly Panel _tokensPanel = new();
    private readonly Label _windowName = new();
    private readonly Label _remaining = new();
    private readonly Label _reset = new();
    private readonly Label _limitError = new();
    private readonly Label _tokenError = new();
    private readonly Label _latestDay = new();
    private readonly Label _latestCount = new();
    private readonly Label _lifetime = new();
    private readonly Label _peak = new();
    private readonly Label _tokenUnavailable = new();
    private readonly Button _refreshLimits = new();
    private readonly Button _install = new();
    private readonly Button _refreshTokens = new();
    private readonly UsageRing _ring = new();
    private readonly UsageSparkline _sparkline = new();
    private readonly Label _historyHeading = new() { Text = "USAGE HISTORY · 24 HOURS" };
    private readonly TokenUsageChart _tokenChart = new();
    private readonly UsageLimitRow[] _otherRows = [new(), new()];
    private readonly ToolTip _errorTip = new();
    private bool _showingTokens;

    public bool ShowingTokens => _showingTokens;
    public bool IsOpen => Visible;

    public event EventHandler? RefreshRequested;
    public event EventHandler? InstallRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? TokensRequested;
    public event EventHandler? TokenRefreshRequested;

    public UsagePopup(PluginInfo plugin)
    {
        _plugin = plugin;
        Text = "Codex Monitor";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(420, 654);
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);
        AccessibleName = "Codex Monitor usage";

        _title.Text = "Codex Monitor";
        ConfigureLabel(_title, 19, 15, 232, 30, 17, FontStyle.Bold);
        _pluginTag.Text = $"{plugin.DisplayName.ToUpperInvariant()} PLUGIN";
        _pluginTag.TextAlign = ContentAlignment.MiddleCenter;
        ConfigureLabel(_pluginTag, 243, 21, 91, 24, 8.5f, FontStyle.Bold, Card, Accent);
        _errorTip.SetToolTip(_pluginTag, $"{plugin.Description} · Windows only");
        _settings.Text = "⚙";
        _settings.SetBounds(340, 18, 31, 29);
        _settings.FlatStyle = FlatStyle.Flat;
        _settings.FlatAppearance.BorderSize = 0;
        _settings.BackColor = Background;
        _settings.ForeColor = Muted;
        _settings.Font = new Font("Segoe UI Symbol", 12);
        _settings.AccessibleName = "Open Codex Monitor settings";
        _settings.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_settings);
        ConfigureLabel(_subtitle, 20, 46, 338, 20, 9.5f, color: Muted);
        _close.Text = "×";
        _close.SetBounds(378, 12, 28, 28);
        _close.AccessibleName = "Close usage window";
        _close.FlatStyle = FlatStyle.Flat;
        _close.FlatAppearance.BorderSize = 0;
        _close.BackColor = Background;
        _close.ForeColor = Muted;
        _close.Font = new Font("Segoe UI", 15);
        _close.Click += (_, _) => Hide();
        Controls.Add(_close);

        StyleTab(_limitsTab, "Limits", 18, 76, 188);
        StyleTab(_tokensTab, "Tokens", 210, 76, 192);
        _tokensTab.Enabled = plugin.SupportsTokens;
        _tokensTab.Visible = plugin.SupportsTokens;
        if (!plugin.SupportsTokens) _limitsTab.Width = 384;
        _limitsTab.Click += (_, _) => SelectView(false);
        _tokensTab.Click += (_, _) => SelectView(true);
        Controls.AddRange([_limitsTab, _tokensTab]);

        _limitsPanel.SetBounds(18, 124, 384, 520);
        _limitsPanel.BackColor = Background;
        Controls.Add(_limitsPanel);
        BuildLimitsPanel();

        _tokensPanel.SetBounds(18, 124, 384, 520);
        _tokensPanel.BackColor = Background;
        Controls.Add(_tokensPanel);
        BuildTokensPanel();
        SelectView(false);

        Deactivate += (_, _) => Hide();
        _title.MouseDown += DragWindow;
        _subtitle.MouseDown += DragWindow;
    }

    public void ShowSnapshot(DashboardState state)
    {
        var snapshot = state.Limits;
        var selected = state.SelectedLimit;
        var history = state.RecentHistory;
        var error = state.LimitError;
        var busy = state.LimitsBusy;
        var cliProblem = state.DependencyProblem;
        var tokenUsage = state.Tokens;
        var tokenError = state.TokenError;
        var tokenBusy = state.TokensBusy;
        var status = state.PluginStatus;
        var statusText = UsageDisplayText.StatusText(status.Health);
        _pluginTag.Text = status.Health switch
        {
            PluginHealth.Ready => $"{_plugin.DisplayName.ToUpperInvariant()} · OK",
            PluginHealth.Refreshing => $"{_plugin.DisplayName.ToUpperInvariant()} · ...",
            PluginHealth.Stale => $"{_plugin.DisplayName.ToUpperInvariant()} · STALE",
            PluginHealth.Missing => $"{_plugin.DisplayName.ToUpperInvariant()} · SETUP",
            PluginHealth.Broken => $"{_plugin.DisplayName.ToUpperInvariant()} · REPAIR",
            PluginHealth.Error => $"{_plugin.DisplayName.ToUpperInvariant()} · ERROR",
            _ => $"{_plugin.DisplayName.ToUpperInvariant()} PLUGIN"
        };
        _pluginTag.ForeColor = StatusColor(status.Health);
        _limitsSubtitle = busy ? $"{statusText} · Refreshing limits..." : snapshot?.Plan is { Length: > 0 } plan
                ? $"{statusText} · {char.ToUpperInvariant(plan[0])}{plan[1..]} plan  ·  {UsageDisplayText.UpdatedText(snapshot.RetrievedAt, status.Health is PluginHealth.Stale or PluginHealth.Error)}"
                : $"{statusText} · {UsageDisplayText.UpdatedText(snapshot?.RetrievedAt, status.Health is PluginHealth.Stale or PluginHealth.Error)}";
        _tokensSubtitle = UsageDisplayText.TokenUpdatedText(tokenUsage?.RetrievedAt, tokenBusy);
        _subtitle.Text = _showingTokens ? _tokensSubtitle : _limitsSubtitle;

        _windowName.Text = selected is null ? "Usage" : UsageDisplayText.WindowName(selected.Name);
        _windowName.AccessibleName = _windowName.Text;
        _ring.UsedPercent = selected?.UsedPercent;
        _remaining.Text = selected is null ? "No data" : $"{100 - selected.UsedPercent:0}% left";
        _remaining.ForeColor = UsageColor.For(selected?.UsedPercent);
        _reset.Text = selected is null ? $"Connect {_plugin.DisplayName} to view your limits." : UsageDisplayText.ResetText(selected.ResetsAt);
        _reset.AccessibleName = _reset.Text.Replace("\n", ", ");
        _sparkline.SetPoints(history);
        _sparkline.AccessibleName = $"{UsageDisplayText.WindowName(selected?.Name ?? "limit")} usage history for the last 24 hours";

        var otherWindows = snapshot?.Windows.Where(w => !ReferenceEquals(w, selected)).Take(2).ToArray() ?? [];
        for (var i = 0; i < _otherRows.Length; i++)
        {
            _otherRows[i].SetWindow(i < otherWindows.Length ? otherWindows[i] : null);
            _otherRows[i].Visible = i < Math.Max(1, otherWindows.Length);
        }
        var historyTop = LogicalToDeviceUnits(178 + Math.Max(1, otherWindows.Length) * 70);
        _historyHeading.Top = historyTop;
        _sparkline.Top = historyTop + LogicalToDeviceUnits(24);
        _sparkline.Height = LogicalToDeviceUnits(415) - _sparkline.Top;

        _limitError.Text = error ?? "";
        _limitError.Visible = error is not null;
        _errorTip.SetToolTip(_limitError, error);
        _refreshLimits.Enabled = !busy;
        _install.Visible = _plugin.CanRepair && cliProblem != DependencyProblem.None;
        _install.Text = cliProblem == DependencyProblem.Broken ? "Repair CLI" : "Install CLI";
        _install.Enabled = !busy;

        UpdateTokenView(tokenUsage, tokenError, tokenBusy);
        _subtitle.Text = _showingTokens
            ? UsageDisplayText.TokenUpdatedText(tokenUsage?.RetrievedAt, tokenBusy)
            : _subtitle.Text;
    }

    public void ShowNearCursor()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        FitToWorkingArea(area);
        Location = new Point(
            Math.Clamp(Cursor.Position.X - Width / 2, area.Left, area.Right - Width),
            Math.Clamp(Cursor.Position.Y - Height - 8, area.Top, area.Bottom - Height));
        Show();
        Activate();
        // Handle creation can change DPI when opening on another monitor.
        FitToWorkingArea(Screen.FromControl(this).WorkingArea);
    }

    internal void FitToWorkingArea(Rectangle area)
    {
        var desired = new Size(LogicalToDeviceUnits(420), LogicalToDeviceUnits(654));
        var available = new Size(Math.Max(1, area.Width - 16), Math.Max(1, area.Height - 16));
        var constrained = desired.Width > available.Width || desired.Height > available.Height;
        AutoScrollMinSize = Size.Empty;
        AutoScrollPosition = Point.Empty;
        AutoScroll = false;
        // This borderless window's outer size includes any scrollbars. Setting
        // ClientSize would add their size and could overflow a short display.
        Size = new Size(Math.Min(desired.Width, available.Width), Math.Min(desired.Height, available.Height));
        AutoScrollMinSize = constrained ? desired : Size.Empty;
        AutoScroll = constrained;
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width),
            Math.Clamp(Top, area.Top, area.Bottom - Height));
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        FitToWorkingArea(Screen.FromRectangle(e.SuggestedRectangle).WorkingArea);
    }

    private void BuildLimitsPanel()
    {
        var card = new Panel { Bounds = new Rectangle(0, 0, 384, 164), BackColor = Card, AccessibleName = "Primary Codex usage limit" };
        _limitsPanel.Controls.Add(card);
        _ring.SetBounds(8, 11, 142, 142);
        _ring.BackColor = Card;
        card.Controls.Add(_ring);
        ConfigureLabel(_windowName, 164, 25, 205, 24, 13, FontStyle.Bold, Card, Color.White, card);
        ConfigureLabel(_remaining, 164, 61, 205, 38, 22, FontStyle.Bold, Card, Color.White, card);
        ConfigureLabel(_reset, 164, 105, 205, 48, 9.5f, backColor: Card, color: Muted, parent: card);

        for (var i = 0; i < _otherRows.Length; i++)
        {
            _otherRows[i].SetBounds(0, 174 + i * 70, 384, 62);
            _limitsPanel.Controls.Add(_otherRows[i]);
        }

        ConfigureLabel(_historyHeading, 0, 318, 384, 19, 9, FontStyle.Bold, color: Muted, parent: _limitsPanel);
        _sparkline.SetBounds(0, 316, 384, 78);
        _limitsPanel.Controls.Add(_sparkline);
        ConfigureLabel(_limitError, 0, 423, 384, 43, 9.5f, color: Color.FromArgb(245, 139, 139), parent: _limitsPanel);
        StyleButton(_refreshLimits, "Refresh limits", 0, 475, 132, _limitsPanel);
        _refreshLimits.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        StyleButton(_install, "Install CLI", 140, 475, 126, _limitsPanel);
        _install.Click += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BuildTokensPanel()
    {
        var latestCard = new Panel { Bounds = new Rectangle(0, 0, 384, 126), BackColor = Card, AccessibleName = "Latest daily token activity" };
        _tokensPanel.Controls.Add(latestCard);
        ConfigureLabel(_latestDay, 15, 15, 354, 19, 9.5f, FontStyle.Bold, Card, Muted, latestCard);
        ConfigureLabel(_latestCount, 14, 42, 356, 51, 28, FontStyle.Bold, Card, Color.White, latestCard);
        ConfigureLabel(_tokenUnavailable, 15, 94, 354, 24, 9.5f, backColor: Card, color: Muted, parent: latestCard);

        var totals = new Panel { Bounds = new Rectangle(0, 136, 384, 76), BackColor = Card, AccessibleName = "Token usage totals" };
        _tokensPanel.Controls.Add(totals);
        ConfigureLabel(_lifetime, 14, 13, 174, 50, 11, FontStyle.Bold, Card, Color.White, totals);
        ConfigureLabel(_peak, 197, 13, 174, 50, 11, FontStyle.Bold, Card, Color.White, totals);

        var chartTitle = new Label { Text = "DAILY TOKENS · 7-DAY WINDOW" };
        ConfigureLabel(chartTitle, 0, 230, 384, 19, 8.5f, FontStyle.Bold, color: Muted, parent: _tokensPanel);
        _tokenChart.SetBounds(0, 253, 384, 162);
        _tokensPanel.Controls.Add(_tokenChart);
        ConfigureLabel(_tokenError, 0, 423, 384, 43, 9.5f, color: Color.FromArgb(245, 139, 139), parent: _tokensPanel);
        StyleButton(_refreshTokens, "Refresh token activity", 0, 475, 196, _tokensPanel);
        _refreshTokens.Click += (_, _) => TokenRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateTokenView(TokenUsageSnapshot? usage, string? error, bool busy)
    {
        var latest = usage?.DailyBuckets.LastOrDefault();
        _latestDay.Text = latest is null ? "LATEST REPORTED DAY" : latest.Date.ToDateTime(TimeOnly.MinValue).ToString("dddd, MMM d", CultureInfo.CurrentCulture).ToUpperInvariant();
        _latestCount.Text = latest is null ? "--" : latest.Tokens.ToString("N0", CultureInfo.CurrentCulture);
        _latestCount.AccessibleName = latest is null ? "No daily token total reported" : $"{latest.Tokens.ToString("N0", CultureInfo.CurrentCulture)} tokens on {latest.Date}";
        _tokenUnavailable.Text = usage is null
            ? busy ? "Loading account activity..." : "Refresh to load account activity"
            : latest is null ? "No daily totals reported for this account." : $"Total tokens reported by {_plugin.DisplayName}";
        _lifetime.Text = $"LIFETIME\n{UsageDisplayText.FormatTokenCount(usage?.LifetimeTokens)}";
        _peak.Text = $"PEAK DAY\n{UsageDisplayText.FormatTokenCount(usage?.PeakDailyTokens)}";
        _lifetime.AccessibleName = $"Lifetime tokens: {UsageDisplayText.FormatTokenCount(usage?.LifetimeTokens)}";
        _peak.AccessibleName = $"Peak daily tokens: {UsageDisplayText.FormatTokenCount(usage?.PeakDailyTokens)}";
        _tokenChart.SetBuckets(usage?.DailyBuckets ?? []);
        _tokenChart.AccessibleName = "Daily token totals: seven calendar days ending on the latest reported day; focus or hover a bar for exact values";
        _tokenError.Text = error ?? "";
        _tokenError.Visible = error is not null;
        _errorTip.SetToolTip(_tokenError, error);
        _refreshTokens.Enabled = !busy;
    }

    internal void SelectView(bool tokens)
    {
        tokens &= _plugin.SupportsTokens;
        _showingTokens = tokens;
        _limitsPanel.Visible = !tokens;
        _tokensPanel.Visible = tokens;
        _tokensPanel.BringToFront();
        if (!tokens) _limitsPanel.BringToFront();
        StyleTabState(_limitsTab, !tokens);
        StyleTabState(_tokensTab, tokens);
        _subtitle.Text = tokens ? _tokensSubtitle : _limitsSubtitle;
        AutoScrollPosition = Point.Empty;
        if (tokens) TokensRequested?.Invoke(this, EventArgs.Empty);
    }

    private void StyleTab(Button button, string text, int x, int y, int width)
    {
        button.Text = text;
        button.SetBounds(x, y, width, 34);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(64, 76, 91);
        button.BackColor = Card;
        button.ForeColor = Muted;
        button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.AccessibleRole = AccessibleRole.PageTab;
    }

    private static void StyleTabState(Button button, bool selected)
    {
        button.BackColor = selected ? Color.FromArgb(45, 67, 73) : Card;
        button.ForeColor = selected ? Color.White : Muted;
        button.FlatAppearance.BorderColor = selected ? Accent : Color.FromArgb(64, 76, 91);
        button.AccessibleDescription = selected ? "Selected" : "Not selected";
    }

    private void StyleButton(Button button, string text, int x, int y, int width, Control parent)
    {
        button.Text = text;
        button.SetBounds(x, y, width, 32);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(77, 95, 113);
        button.BackColor = Card;
        button.ForeColor = Color.White;
        button.Cursor = Cursors.Hand;
        parent.Controls.Add(button);
    }

    private void ConfigureLabel(Label label, int x, int y, int width, int height, float size = 9,
        FontStyle style = FontStyle.Regular, Color? backColor = null, Color? color = null, Control? parent = null)
    {
        label.SetBounds(x, y, width, height);
        label.Font = new Font("Segoe UI", size, style);
        label.BackColor = backColor ?? Background;
        label.ForeColor = color ?? Color.White;
        label.UseMnemonic = false;
        label.AutoEllipsis = true;
        (parent ?? this).Controls.Add(label);
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(Handle, 0xA1, 0x2, 0);
    }

    private static Color StatusColor(PluginHealth health) => health switch
    {
        PluginHealth.Ready => Accent,
        PluginHealth.Refreshing => Color.FromArgb(178, 196, 218),
        PluginHealth.Stale => Color.FromArgb(246, 193, 96),
        PluginHealth.Missing or PluginHealth.Broken or PluginHealth.Error => Color.FromArgb(245, 139, 139),
        _ => Muted
    };

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = RoundedRect(new Rectangle(0, 0, Width, Height), 14);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Color.FromArgb(62, 72, 86));
        using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 14);
        e.Graphics.DrawPath(border, path);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _errorTip.Dispose();
        base.Dispose(disposing);
    }
}
