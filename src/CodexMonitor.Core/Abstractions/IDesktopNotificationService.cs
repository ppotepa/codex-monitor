namespace CodexMonitor;

// Native desktop notifications are optional; host UI always shows a fallback.
internal interface IDesktopNotificationService
{
    Task NotifyAsync(string title, string message, CancellationToken cancellationToken);
}
