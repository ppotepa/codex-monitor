namespace CodexMonitor.Platform.Unix;

// AppleScript directly via ArgumentList; never pass user text to a shell.
internal sealed class MacOsDesktopNotificationService(
    IExecutableLocator locator, IProcessRunner runner) : IDesktopNotificationService
{
    public async Task NotifyAsync(string title, string message, CancellationToken cancellationToken)
    {
        var executable = locator.Find(["osascript"]);
        if (executable is null) return;
        static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", " ").Replace("\n", " ");
        var script = $"display notification \"{Escape(message)}\" with title \"{Escape(title)}\"";
        try
        {
            await runner.RunAsync(executable, ["-e", script],
                TimeSpan.FromSeconds(4), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or
                                   TimeoutException or OperationCanceledException or ProcessLaunchException)
        {
            // Signed bundles may lack notification permission; the in-app fallback remains.
        }
    }
}
