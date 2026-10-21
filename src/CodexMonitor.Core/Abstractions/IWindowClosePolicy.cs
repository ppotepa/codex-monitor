namespace CodexMonitor;

// The shell chooses whether closing a dashboard hides it to the system tray.
internal interface IWindowClosePolicy
{
    bool HideOnClose { get; }
}
