namespace CodexMonitor.Core.Application;

// Provider-neutral facade for one active agent and its presentation snapshot.
internal sealed class UsageCoordinator : IDisposable
{
    private IUsageProvider _provider;
    private readonly IAgentManager _agentManager;
    private readonly MonitorSettings _settings;
    private readonly IUsageDataStore _dataStore;
    private readonly IMonitorSettingsRepository _settingsRepository;
    private readonly CancellationToken _shutdown;
    private readonly UsageLimitsRefreshService _limits;
    private readonly UsageTokenRefreshService _tokens;
    private readonly UsageHistory _history;
    private readonly UsageAlertService _alerts;
    private int _providerGeneration;

    public UsageSnapshot? Snapshot => _limits.Snapshot;
    public TokenUsageSnapshot? TokenUsage => _tokens.Snapshot;
    public string? LimitError => _limits.Error;
    public string? TokenError => _tokens.Error;
    public DependencyProblem DependencyProblem => _limits.DependencyProblem;
    public bool IsBusy => _limits.IsBusy;
    public bool IsTokenBusy => _tokens.IsBusy;
    public ProviderStatus ProviderStatus => _limits.ProviderStatus;
    public ProviderInfo ProviderInfo => _provider.Info;
    public bool ShowingFreshLimits => Snapshot is not null &&
        Snapshot.Status != "stale" && ProviderStatus.Health == ProviderHealth.Ready;

    public string? PersistenceError
    {
        get
        {
            var messages = new[]
            {
                _dataStore.PersistenceError, _limits.PersistenceError, _tokens.PersistenceError,
                _settingsRepository.PersistenceError
            }.Where(message => !string.IsNullOrWhiteSpace(message)).Distinct().ToArray();
            return messages.Length == 0 ? null : string.Join(Environment.NewLine, messages);
        }
    }

    public event Action? Changed;
    public event Action<string, string, UsageAlertSeverity>? AlertRaised;

    public UsageCoordinator(IAgentManager agentManager, MonitorSettings settings,
        UsageHistory history, IUsageDataStore dataStore,
        IMonitorSettingsRepository settingsRepository, CancellationToken shutdown,
        UsageAlertService alerts)
    {
        _agentManager = agentManager;
        _provider = agentManager.Selection.Provider;
        _settings = settings;
        _dataStore = dataStore;
        _settingsRepository = settingsRepository;
        _shutdown = shutdown;
        _history = history;
        _alerts = alerts;
        _limits = new UsageLimitsRefreshService(
            history, settings, settingsRepository, alerts, shutdown);
        _tokens = new UsageTokenRefreshService(
            settings, dataStore, shutdown, NotifyChanged, _provider);
        _limits.Changed += NotifyChanged;
        _limits.AlertRaised += OnAlertRaised;
        _agentManager.Changed += OnAgentChanged;
    }

    public UsageWindow? SelectedWindow => Snapshot?.Windows.FirstOrDefault(window =>
            window.Name.Equals(_settings.TrayWindow, StringComparison.OrdinalIgnoreCase))
        ?? Snapshot?.MainWindow;

    public IReadOnlyList<UsageHistoryPoint> RecentHistory =>
        // History filtering remains in the limits service's domain-owned collection.
        _history.Recent(SelectedWindow?.Name, DateTimeOffset.UtcNow, _provider.Info.Id);

    public DashboardState Dashboard => new(
        Snapshot, SelectedWindow, RecentHistory, LimitError, IsBusy,
        DependencyProblem, TokenUsage, TokenError, IsTokenBusy, ProviderStatus, PersistenceError);

    public void SetTrayWindow(string key) => _settings.TrayWindow = key;
    public void ToggleSnooze() => _alerts.ToggleSnooze(SelectedWindow, _provider.Info.Id);
    public bool IsSnoozed() => _alerts.IsSnoozed(SelectedWindow, _provider.Info.Id);

    public Task RefreshLimitsAsync() => RefreshLimitsCoreAsync(waitForTurn: false);

    private Task RefreshLimitsCoreAsync(bool waitForTurn)
    {
        var provider = _provider;
        var generation = _providerGeneration;
        return _limits.RefreshAsync(provider, waitForTurn, () => IsCurrent(provider, generation));
    }

    public Task RepairAsync()
    {
        var provider = _provider;
        var generation = _providerGeneration;
        return _limits.RepairAsync(provider, () => IsCurrent(provider, generation));
    }

    public Task RefreshTokensAsync(bool force) =>
        RefreshTokensCoreAsync(force, waitForTurn: false);

    private Task RefreshTokensCoreAsync(bool force, bool waitForTurn)
    {
        var provider = _provider;
        var generation = _providerGeneration;
        return _tokens.RefreshAsync(provider, force, waitForTurn,
            () => IsCurrent(provider, generation));
    }

    private bool IsCurrent(IUsageProvider provider, int generation) =>
        ReferenceEquals(_provider, provider) && generation == _providerGeneration;

    private void OnAlertRaised(string title, string message, UsageAlertSeverity severity) =>
        AlertRaised?.Invoke(title, message, severity);

    private void NotifyChanged() => Changed?.Invoke();

    private void OnAgentChanged()
    {
        var selected = _agentManager.Selection.Provider;
        var changedProvider = !string.Equals(
            _provider.Info.Id, selected.Info.Id, StringComparison.OrdinalIgnoreCase);
        _provider = selected;
        _providerGeneration++;

        _limits.SelectProvider(changedProvider);
        _tokens.SelectProvider(selected, changedProvider);
        NotifyChanged();
        _ = RefreshLimitsCoreAsync(waitForTurn: true);
        if (selected is ITokenUsageProvider)
            _ = RefreshTokensCoreAsync(force: true, waitForTurn: true);
    }

    public void Dispose()
    {
        Changed = null;
        AlertRaised = null;
        _agentManager.Changed -= OnAgentChanged;
        _limits.Changed -= NotifyChanged;
        _limits.AlertRaised -= OnAlertRaised;
    }
}
