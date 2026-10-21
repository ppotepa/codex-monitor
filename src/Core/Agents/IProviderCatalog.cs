namespace CodexMonitor.Core.Agents;

public interface IProviderCatalog
{
    IReadOnlyList<IUsageProvider> All { get; }
    IUsageProvider Get(string id);
}

internal sealed class ProviderCatalog(IEnumerable<IUsageProvider> providers) : IProviderCatalog
{
    public IReadOnlyList<IUsageProvider> All { get; } = providers.ToArray();

    public IUsageProvider Get(string id) => All.SingleOrDefault(provider =>
        provider.Info.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"The '{id}' provider is not supported.");
}
