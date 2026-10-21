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
        services.AddSingleton(sp => sp.GetRequiredService<IAgentDiscovery>().Scan());
        services.AddSingleton(sp => AgentCatalog.Select(
            sp.GetRequiredService<AgentCatalogSnapshot>(),
            sp.GetRequiredService<IPluginCatalog>().All,
            sp.GetRequiredService<MonitorSettings>().PreferredAgentId));
        services.AddSingleton(sp => new UsageCoordinator(
            sp.GetRequiredService<AgentSelection>().Plugin,
            sp.GetRequiredService<MonitorSettings>(),
            sp.GetRequiredService<UsageHistory>(),
            sp.GetRequiredService<MonitorDatabase>(),
            sp.GetRequiredService<CancellationTokenSource>().Token,
            sp.GetRequiredService<UsageAlertService>()));
        return services;
    }
}
