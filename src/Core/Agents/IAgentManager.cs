namespace CodexMonitor.Core.Agents;

// Owns the active adapter selection and lets hosts refresh it without
// reconstructing the coordinator or restarting the desktop process.
public interface IAgentManager
{
    AgentCatalogSnapshot Snapshot { get; }
    AgentSelection Selection { get; }
    event Action? Changed;
    void Refresh();
}

internal sealed class AgentManager : IAgentManager
{
    private readonly IAgentDiscovery _discovery;
    private readonly IProviderCatalog _providers;
    private readonly MonitorSettings _settings;

    public AgentCatalogSnapshot Snapshot { get; private set; }
    public AgentSelection Selection { get; private set; }
    public event Action? Changed;

    public AgentManager(IAgentDiscovery discovery, IProviderCatalog providers, MonitorSettings settings)
    {
        _discovery = discovery;
        _providers = providers;
        _settings = settings;
        Snapshot = discovery.Scan();
        Selection = AgentCatalog.Select(Snapshot, providers.All, settings.PreferredAgentId);
    }

    public void Refresh()
    {
        var snapshot = _discovery.Scan();
        var selection = AgentCatalog.Select(snapshot, _providers.All, _settings.PreferredAgentId);
        var changed = !string.Equals(Selection.Provider.Info.Id, selection.Provider.Info.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Selection.DetectedAgent?.ExecutablePath, selection.DetectedAgent?.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Snapshot = snapshot;
        Selection = selection;
        if (changed) Changed?.Invoke();
    }
}
