namespace CodexMonitor.Platform.Unix;

// Freedesktop notifications; absent on some minimal desktop environments.
internal sealed class LinuxDesktopNotificationService(
    IExecutableLocator locator, IProcessRunner runner) : IDesktopNotificationService
{
    public async Task NotifyAsync(string title, string message, CancellationToken cancellationToken)
    {
        var executable = locator.Find(["notify-send"]);
        if (executable is null) return;
        try
        {
            await runner.RunAsync(executable,
                ["--app-name=Codex Monitor", title, message],
                TimeSpan.FromSeconds(4), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or
                                   TimeoutException or OperationCanceledException or ProcessLaunchException)
        {
            // Desktop notifications are best-effort. The app also shows the alert in its window.
        }
    }
}
