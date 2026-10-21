namespace CodexMonitor.Platform.Unix;

// A Linux tray may silently be unavailable even if the tray API succeeds.
// Closing the window must not leave an unopenable background process.
internal sealed class LinuxWindowClosePolicy : IWindowClosePolicy
{
    public bool HideOnClose => false;
}

// On macOS the menu-bar application provides a reliable way to reopen the window.
internal sealed class MacOsWindowClosePolicy : IWindowClosePolicy
{
    public bool HideOnClose => true;
}
