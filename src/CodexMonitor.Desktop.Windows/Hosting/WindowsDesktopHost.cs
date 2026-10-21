namespace CodexMonitor;

// WinForms is an implementation detail of the Windows desktop host.
internal sealed class WindowsDesktopHost(MonitorContext context) : IDesktopHost
{
    public void Run() => Application.Run(context);
}
