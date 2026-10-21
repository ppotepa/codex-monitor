namespace CodexMonitor;

// The UI framework is selected at the composition root, never by Core.
internal interface IDesktopHost
{
    void Run();
}
