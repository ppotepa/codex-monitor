using CodexMonitor.Providers.Codex;
using CodexMonitor.Infrastructure.Projects;
using CodexMonitor.Core.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Desktop;

// Registers the provider-neutral application module. The host selects its provider separately.
internal static class MonitoringServices
{
    public static IServiceCollection AddCodexMonitorApplication(this IServiceCollection services)
    {
        services.AddSingleton<IProviderCatalog>(sp => new ProviderCatalog(sp.GetServices<IUsageProvider>()));
        services.AddSingleton<IAgentDiscovery, AgentDiscovery>();
        services.AddSingleton(sp => sp.GetRequiredService<IMonitorSettingsRepository>().Load());
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton(sp => UsageHistory.Load(sp.GetRequiredService<IUsageDataStore>()));
        services.AddSingleton<CancellationTokenSource>();
        services.AddSingleton<UsageAlertService>();
        services.AddSingleton<IAgentManager, AgentManager>();
        services.AddSingleton(sp => sp.GetRequiredService<IAgentManager>().Snapshot);
        services.AddSingleton(sp => sp.GetRequiredService<IAgentManager>().Selection);
        services.AddSingleton(sp => new UsageCoordinator(
            sp.GetRequiredService<IAgentManager>(),
            sp.GetRequiredService<MonitorSettings>(),
            sp.GetRequiredService<UsageHistory>(),
            sp.GetRequiredService<IUsageDataStore>(),
            sp.GetRequiredService<IMonitorSettingsRepository>(),
            sp.GetRequiredService<CancellationTokenSource>().Token,
            sp.GetRequiredService<UsageAlertService>()));
        return services;
    }
}
