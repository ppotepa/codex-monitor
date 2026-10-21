using CodexMonitor.Infrastructure.Windows;

namespace CodexMonitor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\CodexMonitor", out var firstInstance);
        if (!firstInstance) return;
        ApplicationConfiguration.Initialize();
        var settings = MonitorSettings.Load();
        var agents = AgentDiscovery.Scan();
        var selection = AgentCatalog.Select(agents, PluginRegistry.All, settings.PreferredAgentId);
        Application.Run(new MonitorContext(selection, agents));
    }
}
