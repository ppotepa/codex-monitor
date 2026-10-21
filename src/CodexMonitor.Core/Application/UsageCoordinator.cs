namespace CodexMonitor;

// Shared application state: provider operations and errors never depend on
// either desktop host. Only the active plugin generation may update the UI.
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
    private int _tokenConsecutiveFailures;
    private int _pluginGeneration;
    private string? _databaseWriteError;

    public UsageSnapshot? Snapshot { get; private set; }
    public TokenUsageSnapshot? TokenUsage { get; private set; }
    public string? LimitError { get; private set; }
    public string? TokenError { get; private set; }
    public DependencyProblem DependencyProblem { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsTokenBusy { get; private set; }
    public PluginStatus PluginStatus { get; private set; } = new(PluginHealth.Unknown);
    public PluginInfo PluginInfo => _plugin.Info;
    public bool ShowingFreshLimits => Snapshot is not null &&
        Snapshot.Status != "stale" && PluginStatus.Health == PluginHealth.Ready;

    // Each store reports its own failures. A settings write cannot clear a
    // database error, and neither hides a failed baseline-data recovery.
    public string? PersistenceError
    {
        get
        {
            var messages = new[]
            {
                _database.PersistenceError, _databaseWriteError, _settings.PersistenceError
            }.Where(message => !string.IsNullOrWhiteSpace(message)).Distinct().ToArray();
            return messages.Length == 0 ? null : string.Join(Environment.NewLine, messages);
        }
    }

    public event Action? Changed;
    public event Action<string, string, UsageAlertSeverity>? AlertRaised;

    public UsageCoordinator(IAgentManager agentManager, MonitorSettings settings,
        UsageHistory history, MonitorDatabase database, CancellationToken shutdown,
        UsageAlertService alerts)
    {
        _agentManager = agentManager;
        _plugin = agentManager.Selection.Plugin;
        _settings = settings;
        _history = history;
        _database = database;
        _shutdown = shutdown;
        _alerts = alerts;
        TokenUsage = _database.ReadTokenUsage(_plugin.Info.Id)?.Snapshot;
        _agentManager.Changed += OnAgentChanged;
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

    public Task RefreshLimitsAsync() => RefreshLimitsCoreAsync(waitForTurn: false);

    private async Task RefreshLimitsCoreAsync(bool waitForTurn, int? expectedGeneration = null)
    {
        if (_shutdown.IsCancellationRequested) return;
        bool acquired;
        try
        {
            acquired = await _limitsGate.WaitAsync(
                waitForTurn ? Timeout.InfiniteTimeSpan : TimeSpan.Zero, _shutdown);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        if (!acquired) return;

        try
        {
            if (expectedGeneration is int requested && requested != _pluginGeneration) return;
            var plugin = _plugin;
            var generation = _pluginGeneration;
            IsBusy = true;
            LimitError = null;
            DependencyProblem = DependencyProblem.None;
            PluginStatus = PluginStatus with { Health = PluginHealth.Refreshing, Detail = null };
            NotifyChanged();
            try
            {
                await ReadLimitsAsync(plugin, generation);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
            catch (PluginDependencyException ex)
            {
                if (IsCurrent(plugin, generation)) SetDependencyError(ex);
            }
            catch (Exception ex)
            {
                if (IsCurrent(plugin, generation)) SetReadError(ex.Message);
            }
            finally
            {
                IsBusy = false;
                if (!_shutdown.IsCancellationRequested) NotifyChanged();
            }
        }
        finally
        {
            _limitsGate.Release();
        }
    }

    public async Task RepairAsync()
    {
        if (_shutdown.IsCancellationRequested) return;
        bool acquired;
        try { acquired = await _limitsGate.WaitAsync(0, _shutdown); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        if (!acquired) return;

        try
        {
            // Resolve the optional capability only after the gate is acquired.
            var plugin = _plugin;
            var generation = _pluginGeneration;
            if (plugin is not IRepairableUsagePlugin repairable) return;

            IsBusy = true;
            LimitError = null;
            PluginStatus = PluginStatus with { Health = PluginHealth.Refreshing, Detail = null };
            NotifyChanged();
            try
            {
                await repairable.RepairAsync(_shutdown);
                if (IsCurrent(plugin, generation))
                    await ReadLimitsAsync(plugin, generation);
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
            catch (PluginDependencyException ex)
            {
                if (IsCurrent(plugin, generation)) SetDependencyError(ex);
            }
            catch (Exception ex)
            {
                if (IsCurrent(plugin, generation)) SetReadError(ex.Message);
            }
            finally
            {
                IsBusy = false;
                if (!_shutdown.IsCancellationRequested) NotifyChanged();
            }
        }
        finally { _limitsGate.Release(); }
    }

    public Task RefreshTokensAsync(bool force) =>
        RefreshTokensCoreAsync(force, waitForTurn: false);

    private async Task RefreshTokensCoreAsync(
        bool force, bool waitForTurn, int? expectedGeneration = null)
    {
        if (_shutdown.IsCancellationRequested) return;
        bool acquired;
        try
        {
            acquired = await _tokensGate.WaitAsync(
                waitForTurn ? Timeout.InfiniteTimeSpan : TimeSpan.Zero, _shutdown);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { return; }
        if (!acquired) return;

        try
        {
            if (expectedGeneration is int requested && requested != _pluginGeneration) return;
            var plugin = _plugin;
            var generation = _pluginGeneration;
            if (plugin is not ITokenUsagePlugin tokenProvider) return;

            var now = DateTimeOffset.UtcNow;
            // Failed reads use bounded 1/2/4/5-minute backoff; successful
            // reads retain the user-selected normal refresh cadence.
            var wait = _tokenConsecutiveFailures == 0
                ? TimeSpan.FromMinutes(_settings.TokenRefreshMinutes)
                : TimeSpan.FromMinutes(Math.Min(5, 1 << Math.Min(3, _tokenConsecutiveFailures - 1)));
            if (!force && _tokenLastAttemptAt is { } last && now - last < wait)
                return;

            IsTokenBusy = true;
            TokenError = null;
            _tokenLastAttemptAt = now;
            NotifyChanged();
            try
            {
                var snapshot = await tokenProvider.ReadTokensAsync(_shutdown);
                if (!IsCurrent(plugin, generation)) return;
                TokenUsage = snapshot;
                _tokenConsecutiveFailures = 0;
                try
                {
                    _database.SaveTokenUsage(plugin.Info.Id, snapshot, DateTimeOffset.UtcNow);
                    _databaseWriteError = null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    SetDatabaseError(ex);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (!IsCurrent(plugin, generation)) return;
                _tokenConsecutiveFailures = Math.Min(4, _tokenConsecutiveFailures + 1);
                TokenError = ex.Message;
            }
            finally
            {
                IsTokenBusy = false;
                if (!_shutdown.IsCancellationRequested) NotifyChanged();
            }
        }
        finally { _tokensGate.Release(); }
    }

    private async Task ReadLimitsAsync(IUsagePlugin plugin, int generation)
    {
        var snapshot = await plugin.ReadLimitsAsync(_shutdown);
        if (!IsCurrent(plugin, generation)) return;

        Snapshot = snapshot;
        LimitError = snapshot.Status == "stale"
            ? "Usage data is stale; the CLI could not fetch a fresh result."
            : null;
        DependencyProblem = DependencyProblem.None;
        PluginStatus = PluginStatus with
        {
            Health = snapshot.Status == "stale" ? PluginHealth.Stale : PluginHealth.Ready,
            Detail = LimitError,
            LastSuccessfulAt = snapshot.Status == "stale"
                ? PluginStatus.LastSuccessfulAt ?? snapshot.RetrievedAt
                : snapshot.RetrievedAt
        };

        if (snapshot.Status == "stale") return;
        if (_history.Add(snapshot, DateTimeOffset.UtcNow, plugin.Info.Id))
        {
            try
            {
                _history.Save();
                _databaseWriteError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SetDatabaseError(ex);
            }
        }
        CheckAlerts(snapshot, plugin);
    }

    private bool IsCurrent(IUsagePlugin plugin, int generation) =>
        ReferenceEquals(_plugin, plugin) && generation == _pluginGeneration;

    private void SetDependencyError(PluginDependencyException exception)
    {
        SetReadError(exception.Message);
        DependencyProblem = exception.Problem;
        PluginStatus = PluginStatus with
        {
            Health = exception.Problem == DependencyProblem.Missing
                ? PluginHealth.Missing : PluginHealth.Broken,
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

    private void CheckAlerts(UsageSnapshot snapshot, IUsagePlugin plugin)
    {
        var evaluation = _alerts.Evaluate(snapshot, plugin.Info.DisplayName, plugin.Info.Id);
        foreach (var notification in evaluation.Notifications)
            AlertRaised?.Invoke(notification.Title, notification.Message, notification.Severity);
        if (evaluation.StateChanged) SaveSettings();
    }

    private void SaveSettings()
    {
        // MonitorSettings maintains its own error until that store is written.
        try { _settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void SetDatabaseError(Exception exception) =>
        _databaseWriteError = $"Local database could not be saved: {exception.Message}";

    private void NotifyChanged() => Changed?.Invoke();

    private void OnAgentChanged()
    {
        var selected = _agentManager.Selection.Plugin;
        var changedProvider = !string.Equals(
            _plugin.Info.Id, selected.Info.Id, StringComparison.OrdinalIgnoreCase);
        _plugin = selected;
        var generation = ++_pluginGeneration;

        if (changedProvider)
        {
            Snapshot = null;
            TokenUsage = _database.ReadTokenUsage(selected.Info.Id)?.Snapshot;
            LimitError = null;
            TokenError = null;
            DependencyProblem = DependencyProblem.None;
            PluginStatus = new PluginStatus(PluginHealth.Unknown);
            _tokenLastAttemptAt = null;
            _tokenConsecutiveFailures = 0;
        }

        NotifyChanged();
        // Unlike timer/manual refresh, agent changes wait for an older read to
        // release the gate. Stale generations are skipped without a poll loop.
        _ = RefreshLimitsCoreAsync(waitForTurn: true, expectedGeneration: generation);
        if (changedProvider && selected is ITokenUsagePlugin)
            _ = RefreshTokensCoreAsync(force: true, waitForTurn: true, expectedGeneration: generation);
    }

    public void Dispose()
    {
        Changed = null;
        AlertRaised = null;
        _agentManager.Changed -= OnAgentChanged;
        // In-flight tasks still need to release the semaphores on shutdown.
    }
}
