using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CodexMonitor.UI.Views;

namespace CodexMonitor.Desktop;

// One desktop lifetime, timer and event pipeline for all target systems.
internal sealed class DesktopHost(
    DashboardWindow window,
    DesktopTrayShell tray,
    UsageCoordinator coordinator,
    MonitorSettings settings,
    IMonitorSettingsRepository settingsRepository,
    IAgentManager agentManager,
    IStartupService startup,
    IWindowClosePolicy closePolicy,
    IDesktopNotificationService notifications,
    CodexMonitor.Core.Workspace.IWorkspaceGoalRepository goals,
    CodexMonitor.Core.Workspace.IWorkspaceProjectRepository projects,
    CodexMonitor.Core.Workspace.IWorkspaceInstructionsInspector inspector,
    CodexMonitor.Core.Workspace.IGitImpactService git,
    CodexMonitor.Core.Workspace.IWorkspaceActivitySource activity,
    CodexMonitor.Core.Workspace.IWorkspacePlanner planner,
    CodexMonitor.Core.Workspace.ILocalSkillsService skills,
    CodexMonitor.Core.Workspace.IInstructionsAuditService instructionAudit,
    CodexMonitor.Core.Workspace.IInstructionsOptimizer instructionOptimizer,
    CodexMonitor.Core.Workspace.IInstructionsFileEditor instructionEditor,
    CodexMonitor.Core.Workspace.IWorkspaceDoctor doctor,
    IEnumerable<CodexMonitor.Core.Workspace.IAgentSessionSource> sessionSources,
    CodexMonitor.Core.Workspace.IWorkspaceAiPlanner aiPlanner,
    CancellationTokenSource shutdown) : IDisposable
{
    private WorkspaceWindow? _workspaceWindow;
    private DispatcherTimer? _limitsTimer;
    private DispatcherTimer? _displayTimer;
    private DateTimeOffset _lastAgentScanAt;
    private bool _disposed;
    private bool _trayAvailable;

    public void Run()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            throw new InvalidOperationException("A desktop lifetime is required.");

        lifetime.MainWindow = window;
        window.SelectProvider(agentManager.Selection.Provider.Info);
        window.Closed += (_, _) => Quit(lifetime);
        _lastAgentScanAt = DateTimeOffset.UtcNow;

        window.RefreshRequested += (_, _) => _ = coordinator.RefreshLimitsAsync();
        window.InstallRequested += (_, _) => _ = coordinator.RepairAsync();
        window.SettingsRequested += (_, _) => _ = ShowSettingsAsync();
        window.WorkspaceRequested += (_, _) => ShowWorkspace();
        window.TokensRequested += (_, _) => _ = coordinator.RefreshTokensAsync(force: false);
        window.TokenRefreshRequested += (_, _) => _ = coordinator.RefreshTokensAsync(force: true);

        try
        {
            tray.Install(window.ShowNearCursor, ShowAgents,
                () => _ = ShowSettingsAsync(), () => Quit(lifetime));
            _trayAvailable = true;
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or
                                   NotSupportedException or InvalidOperationException)
        {
            window.SetMessage($"System tray is unavailable: {ex.Message}");
            _trayAvailable = false;
        }
        window.SetTrayAvailable(_trayAvailable);

        tray.Changed += RefreshView;
        tray.ErrorOccurred += ShowStatus;
        coordinator.Changed += RefreshView;
        coordinator.AlertRaised += OnAlert;
        agentManager.Changed += OnAgentChanged;

        _limitsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(settings.LimitsRefreshMinutes)
        };
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
                agentManager.Refresh();
            }
        };
        _displayTimer.Start();
        RefreshView();

        // Windows and macOS start in tray mode. Linux keeps a visible window:
        // even a successfully installed Linux tray may not be shown by Wayland.
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            if (_trayAvailable && closePolicy.HideOnClose) window.Hide();
            else window.ShowNearCursor();
            _ = coordinator.RefreshLimitsAsync();
        });
    }

    private void RefreshView()
    {
        if (_disposed || shutdown.IsCancellationRequested) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshView);
            return;
        }
        var state = coordinator.Dashboard;
        window.ApplyState(state);
        _workspaceWindow?.ApplyUsage(state.Limits, coordinator.ProviderInfo.Id);
        tray.Update(state);
    }

    private void OnAlert(string title, string message, UsageAlertSeverity severity)
    {
        ShowStatus($"{title}: {message}");
        _ = notifications.NotifyAsync(title, message, shutdown.Token);
    }

    private void ShowStatus(string message)
    {
        if (_disposed || shutdown.IsCancellationRequested) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowStatus(message));
            return;
        }
        window.SetMessage(message);
    }

    private void ShowWorkspace()
    {
        if (_disposed) return;
        if (_workspaceWindow is null)
        {
            _workspaceWindow = new WorkspaceWindow(window.WorkspaceUsageModel, goals, projects, inspector, git, activity,
                planner, skills, instructionAudit, instructionOptimizer, instructionEditor, doctor, sessionSources, aiPlanner);
            _workspaceWindow.Closed += (_, _) => _workspaceWindow = null;
        }
        _workspaceWindow.ApplyUsage(coordinator.Dashboard.Limits, coordinator.ProviderInfo.Id);
        _workspaceWindow.Show();
        _workspaceWindow.Activate();
        _ = coordinator.RefreshLimitsAsync();
        _ = coordinator.RefreshTokensAsync(force: false);
    }

    private async Task ShowSettingsAsync()
    {
        if (_disposed) return;
        window.ShowNearCursor();
        var dialog = new SettingsWindow(settings, settingsRepository, startup);
        var saved = await dialog.ShowDialog<bool>(window);
        if (!saved || _disposed) return;

        if (_limitsTimer is not null)
            _limitsTimer.Interval = TimeSpan.FromMinutes(settings.LimitsRefreshMinutes);
        tray.Update(coordinator.Dashboard);
        RefreshView();
        _ = coordinator.RefreshLimitsAsync();
    }

    private void ShowAgents()
    {
        if (_disposed) return;
        window.ShowNearCursor();
        agentManager.Refresh();
        _lastAgentScanAt = DateTimeOffset.UtcNow;
        var dialog = new AgentStatusWindow(agentManager.Snapshot);
        dialog.Show(window);
    }

    private void OnAgentChanged()
    {
        if (_disposed) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnAgentChanged);
            return;
        }
        var selected = agentManager.Selection.Provider.Info;
        window.SelectProvider(selected);
        tray.SetProvider(selected);
        RefreshView();
    }

    private void Quit(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        Dispose();
        lifetime.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        shutdown.Cancel();
        _limitsTimer?.Stop();
        _displayTimer?.Stop();
        coordinator.Changed -= RefreshView;
        coordinator.AlertRaised -= OnAlert;
        tray.Changed -= RefreshView;
        tray.ErrorOccurred -= ShowStatus;
        agentManager.Changed -= OnAgentChanged;
        tray.Dispose();
        _workspaceWindow?.Close();
        window.Dispose();
    }
}
