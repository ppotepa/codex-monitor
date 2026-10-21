namespace CodexMonitor.Core.Abstractions;

// The shell chooses whether closing a dashboard hides it to the system tray.
public interface IWindowClosePolicy
{
    bool HideOnClose { get; }
}
