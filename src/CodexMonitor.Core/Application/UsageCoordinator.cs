namespace CodexMonitor;

// Application-layer coordinator. It knows when data should be read and how a
// provider failure is represented, but it does not know anything about forms,
// tray menus or controls.
internal sealed class UsageCoordinator : IDisposable
{
    private readonly IUsagePlugin _plugin;
    private readonly MonitorSettings _settings;
    private readonly UsageAlertService _alerts;
    private readonly UsageHistory _history;
    private readonly MonitorDatabase _database;
    private readonly CancellationToken _shutdown;
    private bool _autoRecoveryAttempted;
    private DateTimeOffset? _tokenLastAttemptAt;

    public UsageSnapshot? Snapshot { get; private set; }
    public TokenUsageSnapshot? TokenUsage { get; private set; }
    public string? LimitError { get; private set; }
    public string? TokenError { get; private set; }
    public DependencyProblem DependencyProblem { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsTokenBusy { get; private set; }
    public PluginStatus PluginStatus { get; private set; } = new(PluginHealth.Unknown);
    public PluginInfo PluginInfo => _plugin.Info;
    public bool ShowingFreshLimits => Snapshot?.Status != "stale" && Snapshot is not null;

    public event Action? Changed;
    public event Action<string, string, UsageAlertSeverity>? AlertRaised;

    public UsageCoordinator(IUsagePlugin plugin, MonitorSettings settings, UsageHistory history,
        MonitorDatabase database, CancellationToken shutdown, UsageAlertService alerts)
    {
        _plugin = plugin;
        _settings = settings;
        _history = history;
        _database = database;
        _shutdown = shutdown;
        _alerts = alerts;
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
        _history.Recent(SelectedWindow?.Name, DateTimeOffset.UtcNow);

    public DashboardState Dashboard => new(
        Snapshot, SelectedWindow, RecentHistory, LimitError, IsBusy,
        DependencyProblem, TokenUsage, TokenError, IsTokenBusy, PluginStatus);

    public void SetTrayWindow(string key) => _settings.TrayWindow = key;

    public void ToggleSnooze() => _alerts.ToggleSnooze(SelectedWindow);

    public bool IsSnoozed() => _alerts.IsSnoozed(SelectedWindow);

    public async Task RefreshLimitsAsync()
    {
        if (IsBusy || _shutdown.IsCancellationRequested) return;
        IsBusy = true;
        PluginStatus = PluginStatus with { Health = PluginHealth.Refreshing };
        NotifyChanged();
        try
        {
            await ReadLimitsAsync();
        }
        catch (PluginDependencyException ex)
        {
            SetDependencyError(ex);
            if (!_autoRecoveryAttempted && _plugin is IRepairableUsagePlugin repairable)
            {
                _autoRecoveryAttempted = true;
                try
                {
                    await repairable.RepairAsync(_shutdown);
                    await ReadLimitsAsync();
                }
                catch (Exception repairError)
                {
                    LimitError = $"Could not repair the CLI: {repairError.Message}";
                    PluginStatus = PluginStatus with { Health = PluginHealth.Broken, Detail = LimitError };
                }
            }
        }
        catch (Exception ex)
        {
            LimitError = ex.Message;
            PluginStatus = PluginStatus with { Health = PluginHealth.Error, Detail = ex.Message };
        }
        finally
        {
            IsBusy = false;
            NotifyChanged();
        }
    }

    public async Task RepairAsync()
    {
        if (IsBusy || _shutdown.IsCancellationRequested || _plugin is not IRepairableUsagePlugin repairable) return;
        IsBusy = true;
        LimitError = null;
        PluginStatus = PluginStatus with { Health = PluginHealth.Refreshing, Detail = null };
        NotifyChanged();
        try
        {
            await repairable.RepairAsync(_shutdown);
            await ReadLimitsAsync();
        }
        catch (PluginDependencyException ex)
        {
            SetDependencyError(ex);
        }
        catch (Exception ex)
        {
            LimitError = ex.Message;
            PluginStatus = PluginStatus with { Health = PluginHealth.Error, Detail = ex.Message };
        }
        finally
        {
            IsBusy = false;
            NotifyChanged();
        }
    }

    public async Task RefreshTokensAsync(bool force)
    {
        if (IsTokenBusy || _plugin is not ITokenUsagePlugin tokenProvider || _shutdown.IsCancellationRequested) return;
        var now = DateTimeOffset.UtcNow;
        if (!force && _tokenLastAttemptAt is { } attempt &&
            now - attempt < TimeSpan.FromMinutes(_settings.TokenRefreshMinutes)) return;

        IsTokenBusy = true;
        TokenError = null;
        _tokenLastAttemptAt = now;
        NotifyChanged();
        try
        {
            TokenUsage = await tokenProvider.ReadTokensAsync(_shutdown);
            try { _database.SaveTokenUsage(_plugin.Info.Id, TokenUsage, DateTimeOffset.UtcNow); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
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
        }
    }

    private async Task ReadLimitsAsync()
    {
        Snapshot = await _plugin.ReadLimitsAsync(_shutdown);
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
        if (_history.Add(Snapshot, DateTimeOffset.UtcNow))
        {
            try { _history.Save(); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        CheckAlerts(Snapshot);
    }

    private void SetDependencyError(PluginDependencyException exception)
    {
        LimitError = exception.Message;
        DependencyProblem = exception.Problem;
        PluginStatus = PluginStatus with
        {
            Health = exception.Problem == DependencyProblem.Missing ? PluginHealth.Missing : PluginHealth.Broken,
            Detail = exception.Message
        };
    }

    private void CheckAlerts(UsageSnapshot snapshot)
    {
        var evaluation = _alerts.Evaluate(snapshot, _plugin.Info.DisplayName);
        foreach (var notification in evaluation.Notifications)
            AlertRaised?.Invoke(notification.Title, notification.Message, notification.Severity);
        if (evaluation.StateChanged) SaveSettings();
    }

    private void SaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private void NotifyChanged() => Changed?.Invoke();

    public void Dispose()
    {
        Changed = null;
        AlertRaised = null;
    }
}

