namespace CodexMonitor.Core.Usage.Refresh;

// Owns token refresh serialization, cadence, failure backoff and cache writes.
internal sealed class UsageTokenRefreshService(
    MonitorSettings settings,
    IUsageDataStore store,
    CancellationToken shutdown,
    Action changed,
    IUsageProvider initialProvider)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _lastAttemptAt;
    private int _consecutiveFailures;
    private string? _cacheWriteError;

    public TokenUsageSnapshot? Snapshot { get; private set; } =
        store.ReadTokenUsage(initialProvider.Info.Id)?.Snapshot;
    public string? Error { get; private set; }
    public bool IsBusy { get; private set; }
    public string? PersistenceError => _cacheWriteError;

    public void SelectProvider(IUsageProvider provider, bool changedProvider)
    {
        if (!changedProvider) return;
        Snapshot = store.ReadTokenUsage(provider.Info.Id)?.Snapshot;
        Error = null;
        _lastAttemptAt = null;
        _consecutiveFailures = 0;
    }

    public async Task RefreshAsync(IUsageProvider provider, bool force, bool waitForTurn,
        Func<bool> isCurrent)
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
            if (!isCurrent() || provider is not ITokenUsageProvider tokenProvider) return;

            var now = DateTimeOffset.UtcNow;
            var wait = _consecutiveFailures == 0
                ? TimeSpan.FromMinutes(settings.TokenRefreshMinutes)
                : TimeSpan.FromMinutes(Math.Min(5, 1 << Math.Min(3, _consecutiveFailures - 1)));
            if (!force && _lastAttemptAt is { } last && now - last < wait)
                return;

            IsBusy = true;
            Error = null;
            _lastAttemptAt = now;
            changed();
            try
            {
                var snapshot = await tokenProvider.ReadTokensAsync(shutdown);
                if (!isCurrent()) return;
                Snapshot = snapshot;
                _consecutiveFailures = 0;
                try
                {
                    store.SaveTokenUsage(provider.Info.Id, snapshot, DateTimeOffset.UtcNow);
                    _cacheWriteError = null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _cacheWriteError = $"Token cache could not be saved: {ex.Message}";
                }
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (!isCurrent()) return;
                _consecutiveFailures = Math.Min(4, _consecutiveFailures + 1);
                Error = ex.Message;
            }
            finally
            {
                IsBusy = false;
                if (!shutdown.IsCancellationRequested) changed();
            }
        }
        finally { _gate.Release(); }
    }
}
