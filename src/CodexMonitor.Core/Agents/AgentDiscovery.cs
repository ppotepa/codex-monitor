namespace CodexMonitor;

// Shared algorithm; executable lookup comes from the platform host.
internal sealed class AgentDiscovery(IExecutableLocator locator, IPluginCatalog plugins) : IAgentDiscovery
{
    public AgentCatalogSnapshot Scan() => new(
        DateTimeOffset.UtcNow,
        AgentCatalog.Definitions(plugins.All).Select(definition =>
            new DetectedAgent(definition, locator.Find(definition.ExecutableNames))).ToArray());
}
