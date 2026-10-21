using System.Globalization;

namespace CodexMonitor;

// The token dashboard is independent from the limits dashboard and tray host.
internal sealed class TokensDashboardView : DashboardSection
{
    private PluginInfo _plugin;
    private readonly Label _latestDay = new();
    private readonly Label _latestCount = new();
    private readonly Label _tokenUnavailable = new();
    private readonly Label _lifetime = new();
    private readonly Label _peak = new();
    private readonly Label _tokenError = new();
    private readonly Button _refreshTokens = new();
    private readonly TokenUsageChart _chart = new();
    private readonly ToolTip _errors = new();

    public event EventHandler? RefreshRequested;

    public void SetPlugin(PluginInfo plugin) => _plugin = plugin;

    public TokensDashboardView(PluginInfo plugin)
    {
        _plugin = plugin;
        var latestCard = new Panel { Bounds = new Rectangle(0, 0, 384, 126),
            BackColor = Card, AccessibleName = "Latest daily token activity" };
        Controls.Add(latestCard);
        ConfigureLabel(_latestDay, 15, 15, 354, 19, 9.5f, FontStyle.Bold, Card, Muted, latestCard);
        ConfigureLabel(_latestCount, 14, 42, 356, 51, 28, FontStyle.Bold, Card, Color.White, latestCard);
        ConfigureLabel(_tokenUnavailable, 15, 94, 354, 24, 9.5f, backColor: Card, color: Muted, parent: latestCard);

        var totals = new Panel { Bounds = new Rectangle(0, 136, 384, 76),
            BackColor = Card, AccessibleName = "Token usage totals" };
        Controls.Add(totals);
        ConfigureLabel(_lifetime, 14, 13, 174, 50, 11, FontStyle.Bold, Card, Color.White, totals);
        ConfigureLabel(_peak, 197, 13, 174, 50, 11, FontStyle.Bold, Card, Color.White, totals);

        var chartTitle = new Label { Text = "DAILY TOKENS · 7-DAY WINDOW" };
        ConfigureLabel(chartTitle, 0, 230, 384, 19, 8.5f, FontStyle.Bold, color: Muted);
        _chart.SetBounds(0, 253, 384, 162);
        Controls.Add(_chart);
        ConfigureLabel(_tokenError, 0, 423, 384, 43, 9.5f, color: Color.FromArgb(245, 139, 139));
        StyleButton(_refreshTokens, "Refresh token activity", 0, 475, 196, this);
        _refreshTokens.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowSnapshot(DashboardState state)
    {
        var usage = state.Tokens;
        var latest = usage?.DailyBuckets.LastOrDefault();
        _latestDay.Text = latest is null ? "LATEST REPORTED DAY"
            : latest.Date.ToDateTime(TimeOnly.MinValue)
                .ToString("dddd, MMM d", CultureInfo.CurrentCulture).ToUpperInvariant();
        _latestCount.Text = latest is null ? "--" : latest.Tokens.ToString("N0", CultureInfo.CurrentCulture);
        _latestCount.AccessibleName = latest is null ? "No daily token total reported"
            : $"{latest.Tokens.ToString("N0", CultureInfo.CurrentCulture)} tokens on {latest.Date}";
        _tokenUnavailable.Text = usage is null
            ? state.TokensBusy ? "Loading account activity..." : "Refresh to load account activity"
            : latest is null ? "No daily totals reported for this account." : $"Total tokens reported by {_plugin.DisplayName}";
        _lifetime.Text = $"LIFETIME\n{UsageDisplayText.FormatTokenCount(usage?.LifetimeTokens)}";
        _peak.Text = $"PEAK DAY\n{UsageDisplayText.FormatTokenCount(usage?.PeakDailyTokens)}";
        _lifetime.AccessibleName = $"Lifetime tokens: {UsageDisplayText.FormatTokenCount(usage?.LifetimeTokens)}";
        _peak.AccessibleName = $"Peak daily tokens: {UsageDisplayText.FormatTokenCount(usage?.PeakDailyTokens)}";
        _chart.SetBuckets(usage?.DailyBuckets ?? []);
        _chart.AccessibleName = "Daily token totals: seven calendar days ending on the latest reported day; focus or hover a bar for exact values";
        var error = state.TokenError ?? state.PersistenceError;
        _tokenError.Text = error ?? "";
        _tokenError.Visible = error is not null;
        _errors.SetToolTip(_tokenError, error);
        _refreshTokens.Enabled = !state.TokensBusy;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _errors.Dispose();
        base.Dispose(disposing);
    }
}
