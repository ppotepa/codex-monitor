namespace CodexMonitor;

// Owns the active adapter selection and lets hosts refresh it without
// reconstructing the coordinator or restarting the desktop process.
internal interface IAgentManager
{
    AgentCatalogSnapshot Snapshot { get; }
    AgentSelection Selection { get; }
    event Action? Changed;
    void Refresh();
}

internal sealed class AgentManager : IAgentManager
{
    private readonly IAgentDiscovery _discovery;
    private readonly IPluginCatalog _plugins;
    private readonly MonitorSettings _settings;

    public AgentCatalogSnapshot Snapshot { get; private set; }
    public AgentSelection Selection { get; private set; }
    public event Action? Changed;

    public AgentManager(IAgentDiscovery discovery, IPluginCatalog plugins, MonitorSettings settings)
    {
        _discovery = discovery;
        _plugins = plugins;
        _settings = settings;
        Snapshot = discovery.Scan();
        Selection = AgentCatalog.Select(Snapshot, plugins.All, settings.PreferredAgentId);
    }

    public void Refresh()
    {
        var snapshot = _discovery.Scan();
        var selection = AgentCatalog.Select(snapshot, _plugins.All, _settings.PreferredAgentId);
        var changed = !string.Equals(Selection.Plugin.Info.Id, selection.Plugin.Info.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Selection.DetectedAgent?.ExecutablePath, selection.DetectedAgent?.ExecutablePath, StringComparison.OrdinalIgnoreCase);
        Snapshot = snapshot;
        Selection = selection;
        if (changed) Changed?.Invoke();
    }
}
