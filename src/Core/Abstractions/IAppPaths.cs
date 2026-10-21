namespace CodexMonitor.Core.Abstractions;

// The platform host decides where settings and the small JSON database live.
public interface IAppPaths
{
    string DataDirectory { get; }
}
