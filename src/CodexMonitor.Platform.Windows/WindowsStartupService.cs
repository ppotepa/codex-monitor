using System.Runtime.Versioning;

namespace CodexMonitor.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
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
