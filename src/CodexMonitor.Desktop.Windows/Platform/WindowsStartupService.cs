namespace CodexMonitor.Infrastructure.Windows;

internal sealed class WindowsStartupService : IStartupService
{
    public string MenuLabel => "Start with Windows";
    public bool CanConfigure => AutoStart.IsPackagedExecutable;
    public bool Enabled
    {
        get => AutoStart.Enabled;
        set => AutoStart.Enabled = value;
    }
}
