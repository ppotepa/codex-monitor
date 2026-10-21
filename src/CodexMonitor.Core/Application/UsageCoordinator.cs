namespace CodexMonitor;

// Application-layer coordinator. It knows when data should be read and how a
// provider failure is represented, but it does not know anything about forms,
// tray menus or controls.
internal sealed class UsageCoordinator : IDisposable
{
    private IUsagePlugin _plugin;
    private readonly IAgentManager _agentManager;
    private readonly MonitorSettings _settings;
    private readonly UsageAlertService _alerts;
    private readonly UsageHistory _history;
    private readonly MonitorDatabase _database;
    private readonly CancellationToken _shutdown;
    private readonly SemaphoreSlim _limitsGate = new(1, 1);
    private readonly SemaphoreSlim _tokensGate = new(1, 1);
    private DateTimeOffset? _tokenLastAttemptAt;

    public UsageSnapshot? Snapshot { get; private set; }
    public TokenUsageSnapshot? TokenUsage { get; private set; }
    public string? LimitError { get; private set; }
    public string? TokenError { get; private set; }
    public DependencyProblem DependencyProblem { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsTokenBusy { get; private set; }
    public PluginStatus PluginStatus { get; private set; } = new(PluginHealth.Unknown);
    public string? PersistenceError { get; private set; }
    public PluginInfo PluginInfo => _plugin.Info;
    public bool ShowingFreshLimits => Snapshot is not null &&
        Snapshot.Status != "stale" && PluginStatus.Health == PluginHealth.Ready;

    public event Action? Changed;
    public event Action<string, string, UsageAlertSeverity>? AlertRaised;

    public UsageCoordinator(IAgentManager agentManager, MonitorSettings settings, UsageHistory history,
        MonitorDatabase database, CancellationToken shutdown, UsageAlertService alerts)
    {
        _agentManager = agentManager;
        _plugin = agentManager.Selection.Plugin;
        _settings = settings;
        _history = history;
        _database = database;
        _shutdown = shutdown;
        _alerts = alerts;
        PersistenceError = _database.PersistenceError;
        _agentManager.Changed += OnAgentChanged;
        var cachedTokens = _database.ReadTokenUsage(_plugin.Info.Id);
        if (cachedTokens is not null)
        {
            TokenUsage = cachedTokens.Snapshot;
            // The cached snapshot is displayed immediately, but does not defer the first live read.
        }
    }

    public UsageWindow? SelectedWindow => Snapshot?.Windows.FirstOrDefault(window =>
            window.Name.Equals(_settings.TrayWindow, StringComparison.OrdinalIgnoreCase))
        ?? Snapshot?.MainWindow;

    public IReadOnlyList<UsageHistoryPoint> RecentHistory =>
        _history.Recent(SelectedWindow?.Name, DateTimeOffset.UtcNow, _plugin.Info.Id);

    public DashboardState Dashboard => new(
        Snapshot, SelectedWindow, RecentHistory, LimitError, IsBusy,
        DependencyProblem, TokenUsage, TokenError, IsTokenBusy, PluginStatus, PersistenceError);

    public void SetTrayWindow(string key) => _settings.TrayWindow = key;

    public void ToggleSnooze() => _alerts.ToggleSnooze(SelectedWindow, _plugin.Info.Id);

    public bool IsSnoozed() => _alerts.IsSnoozed(SelectedWindow, _plugin.Info.Id);

    public async Task RefreshLimitsAsync()
    {
        if (_shutdown.IsCancellationRequested || !await _limitsGate.WaitAsync(0)) return;
        var plugin = _plugin;
        IsBusy = true;
        LimitError = null;
        DependencyProblem = DependencyProblem.None;
        PluginStatus = PluginStatus with { Health = PluginHealth.Refreshing, Detail = null };
        NotifyChanged();
        try
        {
            await ReadLimitsAsync(plugin);
        }
        catch (PluginDependencyException ex)
        {
            SetDependencyError(ex);
        }
        catch (Exception ex)
        {
            SetReadError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyChanged();
            _limitsGate.Release();
        }
    }

    public async Task RepairAsync()
    {
        if (_shutdown.IsCancellationRequested || _plugin is not IRepairableUsagePlugin repairable ||
            !await _limitsGate.WaitAsync(0)) return;
        var plugin = _plugin;
        IsBusy = true;
        LimitError = null;
        PluginStatus = PluginStatus with { Health = PluginHealth.Refreshing, Detail = null };
        NotifyChanged();
        try
        {
            await repairable.RepairAsync(_shutdown);
            await ReadLimitsAsync(plugin);
        }
        catch (PluginDependencyException ex)
        {
            SetDependencyError(ex);
        }
        catch (Exception ex)
        {
            SetReadError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifyChanged();
            _limitsGate.Release();
        }
    }

    public async Task RefreshTokensAsync(bool force)
    {
        if (_shutdown.IsCancellationRequested || _plugin is not ITokenUsagePlugin tokenProvider ||
            !await _tokensGate.WaitAsync(0)) return;
        var plugin = _plugin;
        var now = DateTimeOffset.UtcNow;
        if (!force && _tokenLastAttemptAt is { } attempt &&
            now - attempt < TimeSpan.FromMinutes(_settings.TokenRefreshMinutes))
        {
            _tokensGate.Release();
            return;
        }

        IsTokenBusy = true;
        TokenError = null;
        _tokenLastAttemptAt = now;
        NotifyChanged();
        try
        {
            var snapshot = await tokenProvider.ReadTokensAsync(_shutdown);
            if (!ReferenceEquals(_plugin, plugin)) return;
            TokenUsage = snapshot;
            try { _database.SaveTokenUsage(plugin.Info.Id, TokenUsage, DateTimeOffset.UtcNow); ClearPersistenceError(); }
            catch (IOException ex) { SetPersistenceError(ex); }
            catch (UnauthorizedAccessException ex) { SetPersistenceError(ex); }
            TokenError = null;
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            TokenError = ex.Message;
        }
        finally
        {
            IsTokenBusy = false;
            if (!_shutdown.IsCancellationRequested) NotifyChanged();
            _tokensGate.Release();
        }
    }

    private async Task ReadLimitsAsync(IUsagePlugin plugin)
    {
        var snapshot = await plugin.ReadLimitsAsync(_shutdown);
        if (!ReferenceEquals(_plugin, plugin)) return;
        Snapshot = snapshot;
        LimitError = Snapshot.Status == "stale"
            ? "Usage data is stale; the CLI could not fetch a fresh result."
            : null;
        DependencyProblem = DependencyProblem.None;
        PluginStatus = PluginStatus with
        {
            Health = Snapshot.Status == "stale" ? PluginHealth.Stale : PluginHealth.Ready,
            Detail = LimitError,
            LastSuccessfulAt = Snapshot.Status == "stale"
                ? PluginStatus.LastSuccessfulAt ?? Snapshot.RetrievedAt
                : Snapshot.RetrievedAt
        };

        if (Snapshot.Status == "stale") return;
        if (_history.Add(Snapshot, DateTimeOffset.UtcNow, _plugin.Info.Id))
        {
            try { _history.Save(); ClearPersistenceError(); }
            catch (IOException ex) { SetPersistenceError(ex); }
            catch (UnauthorizedAccessException ex) { SetPersistenceError(ex); }
        }
        CheckAlerts(Snapshot);
    }

    private void SetDependencyError(PluginDependencyException exception)
    {
        SetReadError(exception.Message);
        DependencyProblem = exception.Problem;
        PluginStatus = PluginStatus with
        {
            Health = exception.Problem == DependencyProblem.Missing ? PluginHealth.Missing : PluginHealth.Broken,
            Detail = exception.Message
        };
    }

    private void SetReadError(string message)
    {
        LimitError = Snapshot is null || PluginStatus.LastSuccessfulAt is null
            ? message
            : $"{message} Showing last successful data from {PluginStatus.LastSuccessfulAt.Value.ToLocalTime():g}.";
        PluginStatus = PluginStatus with { Health = PluginHealth.Error, Detail = message };
    }

    private void CheckAlerts(UsageSnapshot snapshot)
    {
        var evaluation = _alerts.Evaluate(snapshot, _plugin.Info.DisplayName, _plugin.Info.Id);
        foreach (var notification in evaluation.Notifications)
            AlertRaised?.Invoke(notification.Title, notification.Message, notification.Severity);
        if (evaluation.StateChanged) SaveSettings();
    }

    private void SaveSettings()
    {
        try { _settings.Save(); ClearPersistenceError(); }
        catch (IOException ex) { SetPersistenceError(ex); }
        catch (UnauthorizedAccessException ex) { SetPersistenceError(ex); }
    }

    private void SetPersistenceError(Exception exception) =>
        PersistenceError = $"Local data could not be saved: {exception.Message}";

    private void ClearPersistenceError() => PersistenceError = _database.PersistenceError;

    private void NotifyChanged() => Changed?.Invoke();

    private void OnAgentChanged()
    {
        var selected = _agentManager.Selection.Plugin;
        if (string.Equals(_plugin.Info.Id, selected.Info.Id, StringComparison.OrdinalIgnoreCase)) return;
        _plugin = selected;
        Snapshot = null;
        TokenUsage = null;
        LimitError = null;
        TokenError = null;
        DependencyProblem = DependencyProblem.None;
        PluginStatus = new PluginStatus(PluginHealth.Unknown);
        _tokenLastAttemptAt = null;
        NotifyChanged();
    }

    public void Dispose()
    {
        Changed = null;
        AlertRaised = null;
        _agentManager.Changed -= OnAgentChanged;
        // The host cancels the shared shutdown token before disposing the
        // coordinator. In-flight reads still need to reach their finally
        // blocks and release these gates, so they are intentionally left for
        // the GC rather than disposed underneath an active operation.
    }
}

