using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CodexMonitor.Composition;
using CodexMonitor.Infrastructure.Windows;
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
        using var mutex = new Mutex(initiallyOwned: true,
            OperatingSystem.IsWindows() ? @"Local\CodexMonitor" : "CodexMonitor.Desktop",
            out var firstInstance);
        if (!firstInstance) return 0;

        var services = new ServiceCollection();
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IExecutableLocator, WindowsExecutableLocator>();
            services.AddSingleton<ICommandLaunchStrategy, WindowsCommandLaunchStrategy>();
            services.AddSingleton<IAppPaths, WindowsAppPaths>();
            services.AddSingleton<IStartupService, WindowsStartupService>();
            services.AddSingleton<IWindowClosePolicy, WindowsWindowClosePolicy>();
            services.AddSingleton<IDesktopNotificationService, WindowsDesktopNotificationService>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IExecutableLocator, UnixExecutableLocator>();
            services.AddSingleton<ICommandLaunchStrategy, UnixCommandLaunchStrategy>();
            services.AddSingleton<IAppPaths, MacOsAppPaths>();
            services.AddSingleton<IStartupService, MacOsStartupService>();
            services.AddSingleton<IWindowClosePolicy, MacOsWindowClosePolicy>();
            services.AddSingleton<IDesktopNotificationService, MacOsDesktopNotificationService>();
        }
        else if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IExecutableLocator, UnixExecutableLocator>();
            services.AddSingleton<ICommandLaunchStrategy, UnixCommandLaunchStrategy>();
            services.AddSingleton<IAppPaths, LinuxAppPaths>();
            services.AddSingleton<IStartupService, LinuxStartupService>();
            services.AddSingleton<IWindowClosePolicy, LinuxWindowClosePolicy>();
            services.AddSingleton<IDesktopNotificationService, LinuxDesktopNotificationService>();
        }
        else
        {
            Console.Error.WriteLine("Codex Monitor desktop requires Windows, Linux or macOS.");
            return 2;
        }

        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddCodexMonitor();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton(sp => new DashboardWindow(
            sp.GetRequiredService<DashboardViewModel>(),
            sp.GetRequiredService<IWindowClosePolicy>().HideOnClose));
        services.AddSingleton<DesktopTrayShell>();
        services.AddSingleton<DesktopHost>();

        using var provider = services.BuildServiceProvider();
        return AppBuilder.Configure(() => new DesktopApp(provider))
            .UsePlatformDetect()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
}
