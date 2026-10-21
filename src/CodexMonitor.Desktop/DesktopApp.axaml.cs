using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Desktop;

// The same Avalonia application is launched by all three operating systems.
public sealed partial class DesktopApp(IServiceProvider services) : Application
{
    private DesktopHost? _host;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _host = services.GetRequiredService<DesktopHost>();
            lifetime.Exit += (_, _) => _host?.Dispose();
            _host.Run();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
