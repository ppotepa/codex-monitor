using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CodexMonitor.Infrastructure;
using CodexMonitor.Infrastructure.Workspace;
using CodexMonitor.Platform.Windows;
using CodexMonitor.Providers.Codex;
using CodexMonitor.Platform.Unix;
using CodexMonitor.UI.ViewModels;
using CodexMonitor.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Desktop;

// OS choices occur only here; shared Core, Views and ViewModels never branch on OS.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Notify and hook calls are short-lived event writers, not new desktop instances.
        if (args.Length > 0 && args[0] is "--codex-notify" or "--codex-hook")
        {
            var platform = new ServiceCollection();
            if (OperatingSystem.IsWindows()) platform.AddWindowsPlatform();
            else if (OperatingSystem.IsMacOS()) platform.AddMacOsPlatform();
            else if (OperatingSystem.IsLinux()) platform.AddLinuxPlatform();
            else return 2;
            using var platformServices = platform.BuildServiceProvider();
            return WorkspaceNotifyReceiver.Capture(args,
                platformServices.GetRequiredService<IAppPaths>().DataDirectory);
        }
        using var mutex = new Mutex(initiallyOwned: true,
            OperatingSystem.IsWindows() ? @"Local\CodexMonitor" : "CodexMonitor.Desktop",
            out var firstInstance);
        if (!firstInstance) return 0;

        var services = new ServiceCollection();
        if (OperatingSystem.IsWindows())
        {
            services.AddWindowsPlatform();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddMacOsPlatform();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddLinuxPlatform();
        }
        else
        {
            Console.Error.WriteLine("Codex Monitor desktop requires Windows, Linux or macOS.");
            return 2;
        }

        services.AddCodexMonitorInfrastructure();
        services.AddCodexProvider();
        services.AddCodexMonitorApplication();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<WorkspaceViewModel>();
        services.AddSingleton(sp => new DashboardWindow(
            sp.GetRequiredService<DashboardViewModel>(),
            sp.GetRequiredService<IWindowClosePolicy>().HideOnClose));
        services.AddSingleton<WorkspaceWindow>();
        services.AddSingleton<DesktopTrayShell>();
        services.AddSingleton<DesktopHost>();

        using var provider = services.BuildServiceProvider();
        return AppBuilder.Configure(() => new DesktopApp(provider))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
