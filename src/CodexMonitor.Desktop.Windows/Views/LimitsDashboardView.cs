namespace CodexMonitor;

// Owns only the limits view and its controls; refresh/repair are requests to the host.
internal sealed class LimitsDashboardView : DashboardSection
{
    private readonly PluginInfo _plugin;
    private readonly Label _windowName = new();
    private readonly Label _remaining = new();
    private readonly Label _reset = new();
    private readonly Label _limitError = new();
    private readonly Button _refreshLimits = new();
    private readonly Button _install = new();
    private readonly UsageRing _ring = new();
    private readonly UsageSparkline _sparkline = new();
    private readonly Label _historyHeading = new() { Text = "USAGE HISTORY · 24 HOURS" };
    private readonly UsageLimitRow[] _otherRows = [new(), new()];
    private readonly ToolTip _errors = new();

    public event EventHandler? RefreshRequested;
    public event EventHandler? InstallRequested;

    public LimitsDashboardView(PluginInfo plugin)
    {
        _plugin = plugin;
        var card = new Panel { Bounds = new Rectangle(0, 0, 384, 164), BackColor = Card,
            AccessibleName = "Primary Codex usage limit" };
        Controls.Add(card);
        _ring.SetBounds(8, 11, 142, 142);
        _ring.BackColor = Card;
        card.Controls.Add(_ring);
        ConfigureLabel(_windowName, 164, 25, 205, 24, 13, FontStyle.Bold, Card, Color.White, card);
        ConfigureLabel(_remaining, 164, 61, 205, 38, 22, FontStyle.Bold, Card, Color.White, card);
        ConfigureLabel(_reset, 164, 105, 205, 48, 9.5f, backColor: Card, color: Muted, parent: card);

        for (var i = 0; i < _otherRows.Length; i++)
        {
            _otherRows[i].SetBounds(0, 174 + i * 70, 384, 62);
            Controls.Add(_otherRows[i]);
        }

        ConfigureLabel(_historyHeading, 0, 318, 384, 19, 9, FontStyle.Bold, color: Muted);
        _sparkline.SetBounds(0, 316, 384, 78);
        Controls.Add(_sparkline);
        ConfigureLabel(_limitError, 0, 423, 384, 43, 9.5f, color: Color.FromArgb(245, 139, 139));
        StyleButton(_refreshLimits, "Refresh limits", 0, 475, 132, this);
        _refreshLimits.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        StyleButton(_install, "Install CLI", 140, 475, 126, this);
        _install.Click += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowSnapshot(DashboardState state)
    {
        var selected = state.SelectedLimit;
        _windowName.Text = selected is null ? "Usage" : UsageDisplayText.WindowName(selected.Name);
        _windowName.AccessibleName = _windowName.Text;
        _ring.UsedPercent = selected?.UsedPercent;
        _remaining.Text = selected is null ? "No data" : $"{100 - selected.UsedPercent:0}% left";
        _remaining.ForeColor = UsageColor.For(selected?.UsedPercent);
        _reset.Text = selected is null ? $"Connect {_plugin.DisplayName} to view your limits."
            : UsageDisplayText.ResetText(selected.ResetsAt);
        _reset.AccessibleName = _reset.Text.Replace("\n", ", ");
        _sparkline.SetPoints(state.RecentHistory);
        _sparkline.AccessibleName = $"{UsageDisplayText.WindowName(selected?.Name ?? "limit")} usage history for the last 24 hours";

        var otherWindows = state.Limits?.Windows.Where(w => !ReferenceEquals(w, selected)).Take(2).ToArray() ?? [];
        for (var i = 0; i < _otherRows.Length; i++)
        {
            _otherRows[i].SetWindow(i < otherWindows.Length ? otherWindows[i] : null);
            _otherRows[i].Visible = i < Math.Max(1, otherWindows.Length);
        }
        var historyTop = LogicalToDeviceUnits(178 + Math.Max(1, otherWindows.Length) * 70);
        _historyHeading.Top = historyTop;
        _sparkline.Top = historyTop + LogicalToDeviceUnits(24);
        _sparkline.Height = LogicalToDeviceUnits(415) - _sparkline.Top;

        _limitError.Text = state.LimitError ?? "";
        _limitError.Visible = state.LimitError is not null;
        _errors.SetToolTip(_limitError, state.LimitError);
        _refreshLimits.Enabled = !state.LimitsBusy;
        _install.Visible = _plugin.CanRepair && state.DependencyProblem != DependencyProblem.None;
        _install.Text = state.DependencyProblem == DependencyProblem.Broken ? "Repair CLI" : "Install CLI";
        _install.Enabled = !state.LimitsBusy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _errors.Dispose();
        base.Dispose(disposing);
    }
}
