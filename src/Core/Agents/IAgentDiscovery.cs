namespace CodexMonitor.Core.Agents;

public interface IAgentDiscovery
{
    AgentCatalogSnapshot Scan();
}
