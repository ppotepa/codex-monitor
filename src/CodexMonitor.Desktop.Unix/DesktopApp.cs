using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Desktop.Unix;

// AppBuilder uses its factory overload, so DI is explicit and there is no
// mutable static global service locator.
internal sealed class DesktopApp(IServiceProvider services) : Application
{
    private IDesktopHost? _host;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _host = services.GetRequiredService<IDesktopHost>();
            lifetime.Exit += (_, _) => (_host as IDisposable)?.Dispose();
            _host.Run();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
