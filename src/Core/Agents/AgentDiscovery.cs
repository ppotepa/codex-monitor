namespace CodexMonitor.Core.Agents;

// Shared algorithm; executable lookup comes from the platform host.
internal sealed class AgentDiscovery(IExecutableLocator locator, IProviderCatalog providers) : IAgentDiscovery
{
    public AgentCatalogSnapshot Scan() => new(
        DateTimeOffset.UtcNow,
        AgentCatalog.Definitions(providers.All).Select(definition =>
            new DetectedAgent(definition, locator.Find(definition.ExecutableNames))).ToArray());
}
