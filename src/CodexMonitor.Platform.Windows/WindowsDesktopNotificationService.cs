using System.Text;

namespace CodexMonitor.Infrastructure.Windows;

// Windows PowerShell's OS-provided notification-area API is isolated here.
// The main application never references Windows Forms; the short-lived native
// balloon is best-effort and the dashboard always retains its in-app message.
internal sealed class WindowsDesktopNotificationService(IProcessRunner runner)
    : IDesktopNotificationService
{
    public async Task NotifyAsync(string title, string message, CancellationToken cancellationToken)
    {
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(executable)) return;

        var encodedTitle = Convert.ToBase64String(Encoding.Unicode.GetBytes(title));
        var encodedMessage = Convert.ToBase64String(Encoding.Unicode.GetBytes(message));
        var script =
            "Add-Type -AssemblyName System.Windows.Forms; " +
            "$n=New-Object System.Windows.Forms.NotifyIcon; " +
            "$n.Icon=[System.Drawing.SystemIcons]::Information; " +
            "$n.Visible=$true; " +
            "$n.BalloonTipTitle=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('" + encodedTitle + "')); " +
            "$n.BalloonTipText=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('" + encodedMessage + "')); " +
            "$n.ShowBalloonTip(4000); Start-Sleep -Seconds 4; $n.Dispose()";
        try
        {
            await runner.RunAsync(executable,
                ["-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-Command", script],
                TimeSpan.FromSeconds(8), cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                   TimeoutException or OperationCanceledException or
                                   ProcessLaunchException or InvalidOperationException)
        {
            // Native balloons can be disabled. The shared window displays it.
        }
    }
}
