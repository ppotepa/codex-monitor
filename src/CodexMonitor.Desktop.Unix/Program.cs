using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CodexMonitor.Platform.Unix;
using CodexMonitor.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Desktop.Unix;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            Console.Error.WriteLine("Use CodexMonitor.Desktop.Windows on Windows.");
            return 2;
        }

        using var mutex = new Mutex(true, "CodexMonitor.Desktop.Unix", out var firstInstance);
        if (!firstInstance) return 0;

        var services = new ServiceCollection();
        services.AddSingleton<IExecutableLocator, UnixExecutableLocator>();
        services.AddSingleton<ICommandLaunchStrategy, UnixCommandLaunchStrategy>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();

        // This is the only OS selection; all runtime code uses injected services.
        if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IAppPaths, MacOsAppPaths>();
            services.AddSingleton<IStartupService, MacOsStartupService>();
            services.AddSingleton<IWindowClosePolicy, MacOsWindowClosePolicy>();
            services.AddSingleton<IDesktopNotificationService, MacOsDesktopNotificationService>();
        }
        else
        {
            services.AddSingleton<IAppPaths, LinuxAppPaths>();
            services.AddSingleton<IStartupService, LinuxStartupService>();
            services.AddSingleton<IWindowClosePolicy, LinuxWindowClosePolicy>();
            services.AddSingleton<IDesktopNotificationService, LinuxDesktopNotificationService>();
        }

        services.AddCodexMonitor();


        services.AddSingleton(sp => new UnixDashboardWindow(
            sp.GetRequiredService<AgentSelection>().Plugin.Info,
            sp.GetRequiredService<IWindowClosePolicy>()));
        services.AddSingleton<IDashboardView>(sp => sp.GetRequiredService<UnixDashboardWindow>());
        services.AddSingleton<UnixTrayShell>();
        services.AddSingleton<IDesktopHost, UnixDesktopHost>();

        using var provider = services.BuildServiceProvider();
        return AppBuilder.Configure(() => new DesktopApp(provider))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
