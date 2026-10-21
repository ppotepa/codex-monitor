using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Platform.Windows;

public static class WindowsPlatformServices
{
    [SupportedOSPlatform("windows")]
    public static IServiceCollection AddWindowsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IExecutableLocator, WindowsExecutableLocator>();
        services.AddSingleton<ICommandLaunchStrategy, WindowsCommandLaunchStrategy>();
        services.AddSingleton<IAppPaths, WindowsAppPaths>();
        services.AddSingleton<IStartupService, WindowsStartupService>();
        services.AddSingleton<IWindowClosePolicy, WindowsWindowClosePolicy>();
        services.AddSingleton<IDesktopNotificationService, WindowsDesktopNotificationService>();
        return services;
    }
}
