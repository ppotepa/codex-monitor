namespace CodexMonitor;

// A desktop host decides how this view is rendered (WinForms, Avalonia, etc.).
// Shared application code never references a Form, Control or tray library.
internal interface IDashboardView : IDisposable
{
    bool IsOpen { get; }
    bool ShowingTokens { get; }

    event EventHandler? RefreshRequested;
    event EventHandler? InstallRequested;
    event EventHandler? SettingsRequested;
    event EventHandler? TokensRequested;
    event EventHandler? TokenRefreshRequested;

    void ShowSnapshot(DashboardState state);
    void ShowNearCursor();
}
