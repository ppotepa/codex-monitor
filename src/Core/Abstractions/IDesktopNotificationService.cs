namespace CodexMonitor.Core.Abstractions;

// Native desktop notifications are optional; host UI always shows a fallback.
public interface IDesktopNotificationService
{
    Task NotifyAsync(string title, string message, CancellationToken cancellationToken);
}
