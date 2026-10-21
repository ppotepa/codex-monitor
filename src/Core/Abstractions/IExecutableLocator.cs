namespace CodexMonitor.Core.Abstractions;

// Each operating system provides its own lookup rules.
public interface IExecutableLocator
{
    string? Find(IEnumerable<string> executableNames);
}
