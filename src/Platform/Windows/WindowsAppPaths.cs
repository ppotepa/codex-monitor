namespace CodexMonitor.Platform.Windows;

internal sealed class WindowsAppPaths : IAppPaths
{
    public string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexMonitor");
}
