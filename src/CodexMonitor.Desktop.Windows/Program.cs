using CodexMonitor.Infrastructure.Windows;
using CodexMonitor.Plugins.Codex;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor;

// Windows composition root. Future Linux/macOS hosts register their own services.
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\CodexMonitor", out var firstInstance);
        if (!firstInstance) return;
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        services.AddSingleton<IExecutableLocator, WindowsExecutableLocator>();
        services.AddSingleton<ICommandLaunchStrategy, WindowsCommandLaunchStrategy>();
        services.AddSingleton<IAppPaths, WindowsAppPaths>();
        services.AddSingleton<IUsagePlugin, CodexPlugin>();
        services.AddSingleton<IPluginCatalog>(sp => new PluginCatalog(sp.GetServices<IUsagePlugin>()));
        services.AddSingleton<IAgentDiscovery, AgentDiscovery>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        services.AddSingleton(sp => MonitorSettings.Load(Path.Combine(
            sp.GetRequiredService<IAppPaths>().DataDirectory, "settings.json")));
        services.AddSingleton(sp => MonitorDatabase.Load(Path.Combine(
            sp.GetRequiredService<IAppPaths>().DataDirectory, "monitor.db.json")));
        services.AddSingleton(sp => UsageHistory.Load(sp.GetRequiredService<MonitorDatabase>()));
        services.AddSingleton<CancellationTokenSource>();
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
            sp.GetRequiredService<CancellationTokenSource>().Token));
        services.AddSingleton<IDashboardView>(sp => new UsagePopup(
            sp.GetRequiredService<AgentSelection>().Plugin.Info));
        services.AddSingleton<MonitorContext>();
        services.AddSingleton<IDesktopHost, WindowsDesktopHost>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDesktopHost>().Run();
    }
