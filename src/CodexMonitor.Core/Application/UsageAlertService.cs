namespace CodexMonitor;

// Alert state and delivery decisions are independent from refreshing and UI.
internal sealed record UsageNotification(string Title, string Message, UsageAlertSeverity Severity);
internal sealed record AlertEvaluation(bool StateChanged, IReadOnlyList<UsageNotification> Notifications);

internal sealed class UsageAlertService(MonitorSettings settings)
{
    public void ToggleSnooze(UsageWindow? selected)
    {
        if (selected?.ResetsAt is null) return;
        var resetAt = selected.ResetsAt.Value.ToUnixTimeSeconds();
        settings.AlertStates.TryGetValue(selected.Name, out var previous);
        var level = AlertPolicy.Level(selected.UsedPercent, settings.WarningThreshold, settings.CriticalThreshold);
        settings.AlertStates[selected.Name] =
            new WindowAlertState(resetAt, level, !(previous?.Snoozed ?? false));
    }

    public bool IsSnoozed(UsageWindow? selected) =>
        selected is not null &&
        settings.AlertStates.TryGetValue(selected.Name, out var state) &&
        state.Snoozed && state.ResetAt == selected.ResetsAt?.ToUnixTimeSeconds();

    public AlertEvaluation Evaluate(UsageSnapshot snapshot, string pluginName)
    {
        var notifications = new List<UsageNotification>();
        var stateChanged = false;
        foreach (var window in snapshot.Windows)
        {
            var resetAt = window.ResetsAt?.ToUnixTimeSeconds();
            settings.AlertStates.TryGetValue(window.Name, out var previous);
            if (settings.AlertsEnabled && previous is { Level: > 0 } && previous.ResetAt != resetAt)
                notifications.Add(new UsageNotification($"{pluginName} limit reset",
                    $"{window.Name} usage window has reset.", UsageAlertSeverity.Info));

            var level = AlertPolicy.Level(window.UsedPercent,
                settings.WarningThreshold, settings.CriticalThreshold);
            if (settings.AlertsEnabled && AlertPolicy.ShouldNotify(level, previous, resetAt))
                notifications.Add(new UsageNotification($"{pluginName} usage alert",
                    $"{window.Name} limit is {window.UsedPercent:0}% used.", UsageAlertSeverity.Warning));

            var snoozed = previous is not null && previous.ResetAt == resetAt && previous.Snoozed;
            var recordedLevel = previous is not null && previous.ResetAt == resetAt
                ? Math.Max(previous.Level, level) : level;
            if (previous?.ResetAt != resetAt || previous?.Level != recordedLevel ||
                previous?.Snoozed != snoozed)
            {
                settings.AlertStates[window.Name] = new WindowAlertState(resetAt, recordedLevel, snoozed);
                stateChanged = true;
            }
        }
        return new AlertEvaluation(stateChanged, notifications);
    }
}
