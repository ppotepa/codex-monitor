namespace CodexMonitor.Core.Abstractions;

public interface IStartupService
{
    string MenuLabel { get; }
    bool CanConfigure { get; }
    bool Enabled { get; set; }
}
