using CodexMonitor.Infrastructure.Windows;
using CodexMonitor.Composition;
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
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IAppPaths, WindowsAppPaths>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        services.AddCodexMonitor();

        services.AddSingleton<IDashboardView>(sp => new UsagePopup(
            sp.GetRequiredService<AgentSelection>().Plugin.Info));
        services.AddSingleton<MonitorContext>();
        services.AddSingleton<IDesktopHost, WindowsDesktopHost>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IDesktopHost>().Run();
    }
}
