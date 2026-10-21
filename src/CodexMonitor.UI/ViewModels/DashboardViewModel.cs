using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace CodexMonitor.UI.ViewModels;

public sealed record LimitWindowItem(string Label, double UsedPercent, string ResetText);
public sealed record TokenDayItem(string DateText, string CountText, double Percentage);
public sealed record HistorySample(DateTimeOffset At, double UsedPercent);

public sealed class DashboardViewModel : BindableBase
{
    private string _pluginName = "Codex";
    private string _statusText = "Waiting for usage data";
    private string _limitTitle = "Usage";
    private string _remainingText = "No data";
    private string _usedText = "—";
    private string _resetText = "Sign in to Codex CLI to view limits.";
    private double _usedPercent;
    private string _limitError = "";
    private bool _hasLimitError;
    private string _tokenError = "";
    private bool _hasTokenError;
    private string _tokenDate = "Latest reported day";
    private string _latestTokens = "No token data";
    private string _lifetime = "Lifetime: —";
    private string _peak = "Peak day: —";
    private string _notice = "";
    private bool _hasNotice;
    private bool _supportsTokens = true;
    private bool _canRefreshLimits = true;
    private bool _canRefreshTokens = true;
    private bool _showRepair;
    private string _repairLabel = "Install CLI";
    private IReadOnlyList<HistorySample> _history = [];

    public string PluginName { get => _pluginName; private set => Set(ref _pluginName, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string LimitTitle { get => _limitTitle; private set => Set(ref _limitTitle, value); }
    public string RemainingText { get => _remainingText; private set => Set(ref _remainingText, value); }
    public string UsedText { get => _usedText; private set => Set(ref _usedText, value); }
    public string ResetText { get => _resetText; private set => Set(ref _resetText, value); }
    public double UsedPercent { get => _usedPercent; private set => Set(ref _usedPercent, value); }
    public string LimitError { get => _limitError; private set => Set(ref _limitError, value); }
    public bool HasLimitError { get => _hasLimitError; private set => Set(ref _hasLimitError, value); }
    public string TokenError { get => _tokenError; private set => Set(ref _tokenError, value); }
    public bool HasTokenError { get => _hasTokenError; private set => Set(ref _hasTokenError, value); }
    public string TokenDate { get => _tokenDate; private set => Set(ref _tokenDate, value); }
    public string LatestTokens { get => _latestTokens; private set => Set(ref _latestTokens, value); }
    public string Lifetime { get => _lifetime; private set => Set(ref _lifetime, value); }
    public string Peak { get => _peak; private set => Set(ref _peak, value); }
    public string Notice { get => _notice; private set => Set(ref _notice, value); }
    public bool HasNotice { get => _hasNotice; private set => Set(ref _hasNotice, value); }
    public bool SupportsTokens { get => _supportsTokens; private set => Set(ref _supportsTokens, value); }
    public bool CanRefreshLimits { get => _canRefreshLimits; private set => Set(ref _canRefreshLimits, value); }
    public bool CanRefreshTokens { get => _canRefreshTokens; private set => Set(ref _canRefreshTokens, value); }
    public bool ShowRepair { get => _showRepair; private set => Set(ref _showRepair, value); }
    public string RepairLabel { get => _repairLabel; private set => Set(ref _repairLabel, value); }
    public IReadOnlyList<HistorySample> History { get => _history; private set => Set(ref _history, value); }

    public ObservableCollection<LimitWindowItem> OtherLimits { get; } = [];
    public ObservableCollection<TokenDayItem> RecentDays { get; } = [];

    public ICommand RefreshLimitsCommand { get; }
    public ICommand RefreshTokensCommand { get; }
    public ICommand RepairCommand { get; }
    public ICommand SettingsCommand { get; }

    public event Action? RefreshLimitsRequested;
    public event Action? RefreshTokensRequested;
    public event Action? RepairRequested;
    public event Action? SettingsRequested;

    public DashboardViewModel()
    {
        RefreshLimitsCommand = new UiCommand(() => RefreshLimitsRequested?.Invoke());
        RefreshTokensCommand = new UiCommand(() => RefreshTokensRequested?.Invoke());
        RepairCommand = new UiCommand(() => RepairRequested?.Invoke());
        SettingsCommand = new UiCommand(() => SettingsRequested?.Invoke());
    }

    internal void SetPlugin(PluginInfo plugin)
    {
        PluginName = plugin.DisplayName;
        SupportsTokens = plugin.SupportsTokens;
    }

    internal void Apply(DashboardState state, PluginInfo plugin)
    {
        SetPlugin(plugin);
        var selected = state.SelectedLimit;
        StatusText = state.LimitsBusy ? "Refreshing limits…" :
            $"{plugin.DisplayName} · {UsageDisplayText.StatusText(state.PluginStatus.Health)}" +
            (state.Limits?.RetrievedAt is { } date ? $" · {date.ToLocalTime():g}" : "");
        LimitTitle = UsageDisplayText.WindowName(selected?.Name);
        RemainingText = selected is null ? "No data" : $"{100 - selected.UsedPercent:0}% left";
        UsedText = selected is null ? "—" : $"{selected.UsedPercent:0}%";
        UsedPercent = Math.Clamp(selected?.UsedPercent ?? 0, 0, 100);
        ResetText = selected is null
            ? $"Connect {plugin.DisplayName} to view your limits."
            : UsageDisplayText.ResetText(selected.ResetsAt);

        OtherLimits.Clear();
        if (state.Limits is not null)
        {
            foreach (var window in state.Limits.Windows.Where(value => !ReferenceEquals(value, selected)))
                OtherLimits.Add(new LimitWindowItem(
                    UsageDisplayText.WindowName(window.Name),
                    Math.Clamp(window.UsedPercent, 0, 100),
                    UsageDisplayText.ResetShort(window.ResetsAt)));
        }

        History = state.RecentHistory
            .Select(point => new HistorySample(point.At, point.UsedPercent)).ToArray();
        var limitMessage = UsageDisplayText.CombineErrors(state.LimitError, state.PersistenceError);
        LimitError = limitMessage ?? "";
        HasLimitError = !string.IsNullOrWhiteSpace(limitMessage);
        CanRefreshLimits = !state.LimitsBusy;
        ShowRepair = plugin.CanRepair && state.DependencyProblem != DependencyProblem.None;
        RepairLabel = state.DependencyProblem == DependencyProblem.Broken ? "Repair CLI" : "Install CLI";

        var usage = state.Tokens;
        var latest = usage?.DailyBuckets.OrderBy(bucket => bucket.Date).LastOrDefault();
        TokenDate = latest is null ? "Latest reported day" :
            latest.Date.ToString("D", CultureInfo.CurrentCulture);
        LatestTokens = latest is null ? "No token data" :
            UsageDisplayText.FormatTokenCount(latest.Tokens);
        Lifetime = $"LIFETIME · {UsageDisplayText.FormatTokenCount(usage?.LifetimeTokens)}";
        Peak = $"PEAK DAY · {UsageDisplayText.FormatTokenCount(usage?.PeakDailyTokens)}";

        RecentDays.Clear();
        var buckets = usage?.DailyBuckets.OrderBy(value => value.Date).TakeLast(7).ToArray() ?? [];
        var maximum = Math.Max(1L, buckets.Select(value => value.Tokens).DefaultIfEmpty().Max());
        foreach (var bucket in buckets)
            RecentDays.Add(new TokenDayItem(
                bucket.Date.ToString("MMM d", CultureInfo.CurrentCulture),
                $"{bucket.Tokens:N0} tokens",
                Math.Clamp(bucket.Tokens * 100.0 / maximum, 0, 100)));

        var tokenMessage = UsageDisplayText.CombineErrors(state.TokenError, state.PersistenceError);
        TokenError = tokenMessage ?? "";
        HasTokenError = !string.IsNullOrWhiteSpace(tokenMessage);
        CanRefreshTokens = !state.TokensBusy;
    }

    public void SetNotice(string message)
    {
        Notice = message;
        HasNotice = !string.IsNullOrWhiteSpace(message);
    }
}
