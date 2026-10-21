namespace CodexMonitor;

internal interface IAgentDiscovery
{
    AgentCatalogSnapshot Scan();
}
