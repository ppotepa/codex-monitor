namespace CodexMonitor;

// The platform host decides where settings and the small JSON database live.
internal interface IAppPaths
{
    string DataDirectory { get; }
}
