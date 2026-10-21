using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Platform.Unix;

public static class UnixPlatformServices
{
    public static IServiceCollection AddMacOsPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IExecutableLocator, UnixExecutableLocator>();
        services.AddSingleton<ICommandLaunchStrategy, UnixCommandLaunchStrategy>();
        services.AddSingleton<IAppPaths, MacOsAppPaths>();
        services.AddSingleton<IStartupService, MacOsStartupService>();
        services.AddSingleton<IWindowClosePolicy, MacOsWindowClosePolicy>();
        services.AddSingleton<IDesktopNotificationService, MacOsDesktopNotificationService>();
        return services;
    }

    public static IServiceCollection AddLinuxPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IExecutableLocator, UnixExecutableLocator>();
        services.AddSingleton<ICommandLaunchStrategy, UnixCommandLaunchStrategy>();
        services.AddSingleton<IAppPaths, LinuxAppPaths>();
        services.AddSingleton<IStartupService, LinuxStartupService>();
        services.AddSingleton<IWindowClosePolicy, LinuxWindowClosePolicy>();
        services.AddSingleton<IDesktopNotificationService, LinuxDesktopNotificationService>();
        return services;
    }
}
