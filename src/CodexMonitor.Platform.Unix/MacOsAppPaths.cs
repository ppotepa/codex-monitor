namespace CodexMonitor.Platform.Unix;

internal sealed class MacOsAppPaths : IAppPaths
{
    public string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "CodexMonitor");
}
