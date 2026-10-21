using CodexMonitor.Plugins.Codex;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Composition;

// Registers provider-neutral application state and the built-in Codex plugin.
// Desktop hosts only register concrete platform services and views.
internal static class MonitoringServices
{
    public static IServiceCollection AddCodexMonitor(this IServiceCollection services)
    {
        services.AddSingleton<IUsagePlugin, CodexPlugin>();
        services.AddSingleton<IPluginCatalog>(sp => new PluginCatalog(sp.GetServices<IUsagePlugin>()));
        services.AddSingleton<IAgentDiscovery, AgentDiscovery>();
        services.AddSingleton(sp => MonitorSettings.Load(Path.Combine(
            sp.GetRequiredService<IAppPaths>().DataDirectory, "settings.json")));
        services.AddSingleton(sp => MonitorDatabase.Load(Path.Combine(
            sp.GetRequiredService<IAppPaths>().DataDirectory, "monitor.db.json")));
        services.AddSingleton(sp => UsageHistory.Load(sp.GetRequiredService<MonitorDatabase>()));
        services.AddSingleton<CancellationTokenSource>();
        services.AddSingleton<UsageAlertService>();
        services.AddSingleton<IAgentManager, AgentManager>();
        services.AddSingleton(sp => sp.GetRequiredService<IAgentManager>().Snapshot);
        services.AddSingleton(sp => sp.GetRequiredService<IAgentManager>().Selection);
        services.AddSingleton(sp => new UsageCoordinator(
            sp.GetRequiredService<IAgentManager>(),
            sp.GetRequiredService<MonitorSettings>(),
            sp.GetRequiredService<UsageHistory>(),
            sp.GetRequiredService<MonitorDatabase>(),
            sp.GetRequiredService<CancellationTokenSource>().Token,
            sp.GetRequiredService<UsageAlertService>()));
        return services;
    }
}
