using CodexMonitor.Core.Workspace;
using CodexMonitor.Infrastructure.Workspace;
using Microsoft.Extensions.DependencyInjection;
using CodexMonitor.Infrastructure.Persistence;
using CodexMonitor.Infrastructure.Processes;

namespace CodexMonitor.Infrastructure;

public static class InfrastructureServices
{
    public static IServiceCollection AddCodexMonitorInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IWorkspaceGoalRepository>(sp => new JsonWorkspaceGoalRepository(
            Path.Combine(sp.GetRequiredService<IAppPaths>().DataDirectory, "workspace-goals.json")));
        services.AddSingleton<IMonitorSettingsRepository>(sp => new JsonMonitorSettingsRepository(
            Path.Combine(sp.GetRequiredService<IAppPaths>().DataDirectory, "settings.json")));
        services.AddSingleton(sp => MonitorDatabase.Load(Path.Combine(
            sp.GetRequiredService<IAppPaths>().DataDirectory, "monitor.db.json")));
        services.AddSingleton<IUsageDataStore>(sp => sp.GetRequiredService<MonitorDatabase>());
        return services;
    }
}
