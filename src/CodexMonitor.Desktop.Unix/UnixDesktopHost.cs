using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;

namespace CodexMonitor.Desktop.Unix;

// Owns the Avalonia UI lifecycle; the application coordinator remains OS-neutral.
internal sealed class UnixDesktopHost(
    UnixDashboardWindow window,
    UnixTrayShell tray,
    UsageCoordinator coordinator,
    MonitorSettings settings,
    IAgentDiscovery discovery,
    CancellationTokenSource shutdown) : IDesktopHost, IDisposable
{
    private DispatcherTimer? _limitsTimer;
    private DispatcherTimer? _displayTimer;
    private DateTimeOffset _lastAgentScanAt;
    private bool _disposed;

    public void Run()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("A desktop lifetime is required.");

        desktop.MainWindow = window;
        _lastAgentScanAt = DateTimeOffset.UtcNow;

        window.RefreshRequested += (_, _) => _ = coordinator.RefreshLimitsAsync();
        window.InstallRequested += (_, _) => _ = coordinator.RepairAsync();
        window.SettingsRequested += (_, _) => _ = ShowSettingsAsync();
        window.TokensRequested += (_, _) => _ = coordinator.RefreshTokensAsync(force: false);
        window.TokenRefreshRequested += (_, _) => _ = coordinator.RefreshTokensAsync(force: true);

        try
        {
            tray.Install(window.ShowNearCursor, ShowAgents,
                () => _ = ShowSettingsAsync(), () => Quit(desktop));
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException or InvalidOperationException)
        {
            // The window is still the main entry point on Wayland, bare GNOME,
            // or desktops without StatusNotifierItem/AppIndicator integration.
            window.SetMessage($"System tray is unavailable: {ex.Message}");
        }
        tray.Changed += RefreshView;
        tray.ErrorOccurred += ShowStatus;
        coordinator.Changed += RefreshView;
        coordinator.AlertRaised += (title, message, _) => ShowStatus($"{title}: {message}");

        _limitsTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.LimitsRefreshMinutes) };
        _limitsTimer.Tick += (_, _) => _ = coordinator.RefreshLimitsAsync();
        _limitsTimer.Start();
        _displayTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _displayTimer.Tick += (_, _) =>
        {
            RefreshView();
            if (settings.AutoRefreshTokens && window.IsOpen && window.ShowingTokens)
                _ = coordinator.RefreshTokensAsync(force: false);
            if (settings.AgentScanMinutes > 0 &&
                DateTimeOffset.UtcNow - _lastAgentScanAt >= TimeSpan.FromMinutes(settings.AgentScanMinutes))
            {
                _lastAgentScanAt = DateTimeOffset.UtcNow;
                discovery.Scan();
            }
        };
        _displayTimer.Start();
        RefreshView();
        Dispatcher.UIThread.Post(() => _ = coordinator.RefreshLimitsAsync());
    }

    private void RefreshView()
    {
        if (shutdown.IsCancellationRequested || _disposed) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshView);
            return;
        }
        var state = coordinator.Dashboard;
        window.ShowSnapshot(state);
        tray.Update(state);
    }

    private void ShowStatus(string message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowStatus(message));
            return;
        }
        window.SetMessage(message);
        tray.Update(coordinator.Dashboard);
    }

    private async Task ShowSettingsAsync()
    {
        var dialog = new UnixSettingsWindow(settings);
        var saved = await dialog.ShowDialog<bool>(window);
        if (!saved || _disposed) return;
        if (_limitsTimer is not null)
            _limitsTimer.Interval = TimeSpan.FromMinutes(settings.LimitsRefreshMinutes);
        tray.Update(coordinator.Dashboard);
        _ = coordinator.RefreshLimitsAsync();
    }

    private void ShowAgents()
    {
        var agents = discovery.Scan();
        _lastAgentScanAt = DateTimeOffset.UtcNow;
        var rows = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
        foreach (var agent in agents.Agents)
        {
            rows.Children.Add(new TextBlock
            {
                Text = $"{agent.Definition.DisplayName}: " +
                       (agent.IsInstalled ? "Detected" : "Not found") + " · " +
                       (agent.IsSupported ? "Supported" : "No plugin") +
                       (agent.ExecutablePath is null ? "" : $"\n{agent.ExecutablePath}"),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            });
        }
        var dialog = new Window
        {
            Title = "AI agents",
            Width = 430,
            Height = 460,
            Content = new ScrollViewer { Content = rows },
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.Show(window);
    }

    private void Quit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Dispose();
        desktop.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        shutdown.Cancel();
        _limitsTimer?.Stop();
        _displayTimer?.Stop();
        coordinator.Changed -= RefreshView;
        tray.Changed -= RefreshView;
        tray.ErrorOccurred -= ShowStatus;
        tray.Dispose();
        window.Dispose();
    }
}
