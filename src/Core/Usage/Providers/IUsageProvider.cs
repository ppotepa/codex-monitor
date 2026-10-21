using System.Reflection;

namespace CodexMonitor.Core.Usage.Providers;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UsageProviderAttribute(string id, string displayName) : Attribute
{
    public string Id { get; } = id;
    public string DisplayName { get; } = displayName;
    public string Description { get; set; } = "Built-in provider";
    public string DataSource { get; set; } = "";
    public string[] ExecutableNames { get; set; } = [];
}

public sealed record UsageWindowOption(string Key, string Label);

public sealed record ProviderInfo(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<UsageWindowOption> WindowOptions,
    bool SupportsTokens,
    bool CanRepair,
    IReadOnlyList<string> ExecutableNames,
    string DataSource)
{
    public static ProviderInfo From<T>(IReadOnlyList<UsageWindowOption> windows) where T : IUsageProvider
    {
        var metadata = typeof(T).GetCustomAttribute<UsageProviderAttribute>()
            ?? throw new InvalidOperationException($"Missing provider metadata: {typeof(T).Name}");
        return new(metadata.Id, metadata.DisplayName, metadata.Description, windows,
            typeof(ITokenUsageProvider).IsAssignableFrom(typeof(T)),
            typeof(IRepairableUsageProvider).IsAssignableFrom(typeof(T)),
            metadata.ExecutableNames, metadata.DataSource);
    }
}

public enum ProviderHealth
{
    Unknown,
    Ready,
    Refreshing,
    Stale,
    Missing,
    Broken,
    Error
}

public sealed record ProviderStatus(ProviderHealth Health, string? Detail = null,
    DateTimeOffset? LastSuccessfulAt = null);

// Only limits are mandatory; other capabilities are explicit interfaces.
public interface IUsageProvider
{
    ProviderInfo Info { get; }
    Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken);
}

public interface ITokenUsageProvider
{
    Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken);
}

public interface IRepairableUsageProvider
{
    Task RepairAsync(CancellationToken cancellationToken);
}

public enum DependencyProblem { None, Missing, Broken }

public sealed class ProviderDependencyException(DependencyProblem problem, string message) : Exception(message)
{
    public DependencyProblem Problem { get; } = problem;
}
