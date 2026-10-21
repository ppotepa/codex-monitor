namespace CodexMonitor;

internal sealed record UsageWindowOption(string Key, string Label);

internal sealed record PluginInfo(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<UsageWindowOption> WindowOptions,
    bool SupportsTokens,
    bool CanRepair);

internal enum PluginHealth
{
    Unknown,
    Ready,
    Refreshing,
    Stale,
    Missing,
    Broken,
    Error
}

internal sealed record PluginStatus(PluginHealth Health, string? Detail = null,
    DateTimeOffset? LastSuccessfulAt = null);

// Plugins own transport, wire formats and dependencies. The shell consumes only
// normalized snapshots and capabilities; it never launches provider commands.
internal interface IUsagePlugin
{
    PluginInfo Info { get; }
    Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken);
    Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("This plugin does not provide token activity.");
    Task RepairAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException("This plugin does not support dependency repair.");
}

internal enum DependencyProblem { None, Missing, Broken }

internal sealed class PluginDependencyException(DependencyProblem problem, string message) : Exception(message)
{
    public DependencyProblem Problem { get; } = problem;
}
