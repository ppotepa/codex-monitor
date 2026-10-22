using CodexMonitor.Core.Usage;
using CodexMonitor.Core.Usage.Providers;

namespace CodexMonitor.Core.Workspace;

public interface ILimitsFeature : IFeature
{
    Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken);
}

public interface ITokenUsageFeature : IFeature
{
    Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken);
}

public interface IDependencyRepairFeature : IFeature
{
    Task RepairAsync(CancellationToken cancellationToken);
}

// Transitional adapters keep existing refresh services intact while making provider capabilities discoverable.
public sealed class LimitsFeatureAdapter(IUsageProvider provider) : ILimitsFeature
{
    public string ProviderId => provider.Info.Id;
    public string FeatureId => "usage.limits";
    public int Priority => 10;
    public Task<UsageSnapshot> ReadLimitsAsync(CancellationToken ct) => provider.ReadLimitsAsync(ct);
}

public sealed class TokenUsageFeatureAdapter(string providerId, ITokenUsageProvider provider) : ITokenUsageFeature
{
    public string ProviderId => providerId;
    public string FeatureId => "usage.tokens";
    public int Priority => 10;
    public Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken ct) => provider.ReadTokensAsync(ct);
}

public sealed class DependencyRepairFeatureAdapter(string providerId, IRepairableUsageProvider provider) : IDependencyRepairFeature
{
    public string ProviderId => providerId;
    public string FeatureId => "dependency.repair";
    public int Priority => 10;
    public Task RepairAsync(CancellationToken ct) => provider.RepairAsync(ct);
}
