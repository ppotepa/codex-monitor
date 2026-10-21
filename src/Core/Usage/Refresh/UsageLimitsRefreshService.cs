namespace CodexMonitor.Core.Usage.Refresh;

// Owns serialized limits reads, repair, history sampling and alert persistence.
internal sealed class UsageLimitsRefreshService(
    UsageHistory history,
    MonitorSettings settings,
    IMonitorSettingsRepository settingsRepository,
    UsageAlertService alerts,
    CancellationToken shutdown)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _historyWriteError;
    private bool _historyHasPendingChanges;

    public UsageSnapshot? Snapshot { get; private set; }
    public string? Error { get; private set; }
    public DependencyProblem DependencyProblem { get; private set; }
    public bool IsBusy { get; private set; }
    public ProviderStatus ProviderStatus { get; private set; } = new(ProviderHealth.Unknown);
    public string? PersistenceError => _historyWriteError;

    public event Action? Changed;
    public event Action<string, string, UsageAlertSeverity>? AlertRaised;

    public void SelectProvider(bool changedProvider)
    {
        if (!changedProvider) return;
        Snapshot = null;
        Error = null;
        DependencyProblem = DependencyProblem.None;
        ProviderStatus = new ProviderStatus(ProviderHealth.Unknown);
    }

    public Task RefreshAsync(IUsageProvider provider, bool waitForTurn, Func<bool> isCurrent) =>
        RunAsync(provider, waitForTurn, isCurrent, repair: false);

    public Task RepairAsync(IUsageProvider provider, Func<bool> isCurrent) =>
        RunAsync(provider, waitForTurn: false, isCurrent, repair: true);

    private async Task RunAsync(IUsageProvider provider, bool waitForTurn, Func<bool> isCurrent,
        bool repair)
    {
        if (shutdown.IsCancellationRequested) return;
        bool acquired;
        try
        {
            acquired = await _gate.WaitAsync(
                waitForTurn ? Timeout.InfiniteTimeSpan : TimeSpan.Zero, shutdown);
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return; }
        if (!acquired) return;

        try
        {
            if (!isCurrent()) return;
            if (repair && provider is not IRepairableUsageProvider) return;

            IsBusy = true;
            Error = null;
            if (repair)
                ProviderStatus = ProviderStatus with { Health = ProviderHealth.Refreshing, Detail = null };
            else
            {
                DependencyProblem = DependencyProblem.None;
                ProviderStatus = ProviderStatus with { Health = ProviderHealth.Refreshing, Detail = null };
            }
            Changed?.Invoke();

            try
            {
                if (repair)
                    await ((IRepairableUsageProvider)provider).RepairAsync(shutdown);
                if (isCurrent())
                    await ReadLimitsAsync(provider, isCurrent);
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
            catch (ProviderDependencyException ex)
            {
                if (isCurrent()) SetDependencyError(ex);
            }
            catch (Exception ex)
            {
                if (isCurrent()) SetReadError(ex.Message);
            }
            finally
            {
                IsBusy = false;
                if (!shutdown.IsCancellationRequested) Changed?.Invoke();
            }
        }
        finally { _gate.Release(); }
    }

    private async Task ReadLimitsAsync(IUsageProvider provider, Func<bool> isCurrent)
    {
        var snapshot = await provider.ReadLimitsAsync(shutdown);
        if (!isCurrent()) return;

        Snapshot = snapshot;
        Error = snapshot.Status == "stale"
            ? "Usage data is stale; the CLI could not fetch a fresh result."
            : null;
        DependencyProblem = DependencyProblem.None;
        ProviderStatus = ProviderStatus with
        {
            Health = snapshot.Status == "stale" ? ProviderHealth.Stale : ProviderHealth.Ready,
            Detail = Error,
            LastSuccessfulAt = snapshot.Status == "stale"
                ? ProviderStatus.LastSuccessfulAt ?? snapshot.RetrievedAt
                : snapshot.RetrievedAt
        };

        if (snapshot.Status == "stale") return;
        if (history.Add(snapshot, DateTimeOffset.UtcNow, provider.Info.Id))
            _historyHasPendingChanges = true;
        if (_historyHasPendingChanges)
        {
            try
            {
                history.Save();
                _historyHasPendingChanges = false;
                _historyWriteError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _historyWriteError = $"Usage history could not be saved: {ex.Message}";
            }
        }
        CheckAlerts(snapshot, provider);
    }

    private void SetDependencyError(ProviderDependencyException exception)
    {
        SetReadError(exception.Message);
        DependencyProblem = exception.Problem;
        ProviderStatus = ProviderStatus with
        {
            Health = exception.Problem == DependencyProblem.Missing
                ? ProviderHealth.Missing : ProviderHealth.Broken,
            Detail = exception.Message
        };
    }

    private void SetReadError(string message)
    {
        Error = Snapshot is null || ProviderStatus.LastSuccessfulAt is null
            ? message
            : $"{message} Showing last successful data from {ProviderStatus.LastSuccessfulAt.Value.ToLocalTime():g}.";
        ProviderStatus = ProviderStatus with { Health = ProviderHealth.Error, Detail = message };
    }

    private void CheckAlerts(UsageSnapshot snapshot, IUsageProvider provider)
    {
        var evaluation = alerts.Evaluate(snapshot, provider.Info.DisplayName, provider.Info.Id);
        foreach (var notification in evaluation.Notifications)
            AlertRaised?.Invoke(notification.Title, notification.Message, notification.Severity);
        if (evaluation.StateChanged) SaveSettings();
    }

    private void SaveSettings()
    {
        try { settingsRepository.Save(settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
