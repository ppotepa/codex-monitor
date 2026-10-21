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
    private readonly LimitsDashboardView _limitsPanel;
    private readonly TokensDashboardView _tokensPanel;
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
        _limitsPanel = new LimitsDashboardView(plugin);
        _tokensPanel = new TokensDashboardView(plugin);
        _limitsPanel.RefreshRequested += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        _limitsPanel.InstallRequested += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
        _tokensPanel.RefreshRequested += (_, _) => TokenRefreshRequested?.Invoke(this, EventArgs.Empty);

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

        Controls.Add(_limitsPanel);
        Controls.Add(_tokensPanel);
        SelectView(false);

        Deactivate += (_, _) => Hide();
        _title.MouseDown += DragWindow;
        _subtitle.MouseDown += DragWindow;
    }

    public void ShowSnapshot(DashboardState state)
    {
        var snapshot = state.Limits;
        var busy = state.LimitsBusy;
        var tokenUsage = state.Tokens;
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

        _limitsPanel.ShowSnapshot(state);
        _tokensPanel.ShowSnapshot(state);
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
