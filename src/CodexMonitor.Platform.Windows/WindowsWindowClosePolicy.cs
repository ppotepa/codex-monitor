namespace CodexMonitor.Infrastructure.Windows;

// Tray is always present on a normal Windows desktop; closing hides the popup.
internal sealed class WindowsWindowClosePolicy : IWindowClosePolicy
{
    public bool HideOnClose => true;
}
