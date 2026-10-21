namespace CodexMonitor;

internal interface IPluginCatalog
{
    IReadOnlyList<IUsagePlugin> All { get; }
    IUsagePlugin Get(string id);
}

internal sealed class PluginCatalog(IEnumerable<IUsagePlugin> plugins) : IPluginCatalog
{
    public IReadOnlyList<IUsagePlugin> All { get; } = plugins.ToArray();

    public IUsagePlugin Get(string id) => All.SingleOrDefault(plugin =>
        plugin.Info.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"The '{id}' plugin is not supported.");
}
