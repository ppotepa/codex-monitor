using System.Reflection;

namespace CodexMonitor;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class UsagePluginAttribute(string id, string displayName) : Attribute
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
    public string Description { get; set; } = "Built-in plugin";
    public string DataSource { get; set; } = "";
    public string[] ExecutableNames { get; set; } = [];
}

internal sealed record UsageWindowOption(string Key, string Label);

internal sealed record PluginInfo(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<UsageWindowOption> WindowOptions,
    bool SupportsTokens,
    bool CanRepair,
    IReadOnlyList<string> ExecutableNames,
    string DataSource)
{
    public static PluginInfo From<T>(IReadOnlyList<UsageWindowOption> windows) where T : IUsagePlugin
    {
        var metadata = typeof(T).GetCustomAttribute<UsagePluginAttribute>()
            ?? throw new InvalidOperationException($"Missing plugin metadata: {typeof(T).Name}");
        return new(metadata.Id, metadata.DisplayName, metadata.Description, windows,
            typeof(ITokenUsagePlugin).IsAssignableFrom(typeof(T)),
            typeof(IRepairableUsagePlugin).IsAssignableFrom(typeof(T)),
            metadata.ExecutableNames, metadata.DataSource);
    }
}

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

// Only limits are mandatory; other capabilities are explicit interfaces.
internal interface IUsagePlugin
{
    PluginInfo Info { get; }
    Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken);
}

internal interface ITokenUsagePlugin
{
    Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken);
}

internal interface IRepairableUsagePlugin
{
    Task RepairAsync(CancellationToken cancellationToken);
}

internal enum DependencyProblem { None, Missing, Broken }

internal sealed class PluginDependencyException(DependencyProblem problem, string message) : Exception(message)
{
    public DependencyProblem Problem { get; } = problem;
}
