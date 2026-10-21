namespace CodexMonitor;

internal interface IStartupService
{
    string MenuLabel { get; }
    bool CanConfigure { get; }
    bool Enabled { get; set; }
}
