namespace CodexMonitor;

// Each operating system provides its own lookup rules.
internal interface IExecutableLocator
{
    string? Find(IEnumerable<string> executableNames);
}
