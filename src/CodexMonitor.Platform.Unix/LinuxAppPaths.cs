namespace CodexMonitor.Platform.Unix;

internal sealed class LinuxAppPaths : IAppPaths
{
    public string DataDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var root = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdg;
            return Path.Combine(root, "codex-monitor");
        }
    }
}
