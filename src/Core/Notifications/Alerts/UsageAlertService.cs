namespace CodexMonitor.Core.Notifications.Alerts;

// Alert state is separate from usage refresh, UI frameworks and provider parsers.
internal sealed record UsageNotification(string Title, string Message, UsageAlertSeverity Severity);
internal sealed record AlertEvaluation(bool StateChanged, IReadOnlyList<UsageNotification> Notifications);

internal sealed class UsageAlertService(MonitorSettings settings)
{
    private static string Key(string providerId, string windowName) => $"{providerId}:{windowName}";

    private WindowAlertState? Read(string providerId, string windowName) =>
        settings.AlertStates.TryGetValue(Key(providerId, windowName), out var state) ? state : null;

    public void ToggleSnooze(UsageWindow? selected, string providerId)
    {
        if (selected?.ResetsAt is null) return;
        var resetAt = selected.ResetsAt.Value.ToUnixTimeSeconds();
        var previous = Read(providerId, selected.Name);
        var level = AlertPolicy.Level(selected.UsedPercent,
            settings.WarningThreshold, settings.CriticalThreshold);
        var previousCycleExpired = previous?.ResetAt is long old &&
            old <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() && resetAt > old;
        var priorLevel = previousCycleExpired ? 0 : previous?.Level ?? 0;
        var wasSnoozed = !previousCycleExpired && previous?.Snoozed == true;
        settings.AlertStates[Key(providerId, selected.Name)] =
            new WindowAlertState(resetAt, Math.Max(priorLevel, level), !wasSnoozed);
    }

    public bool IsSnoozed(UsageWindow? selected, string providerId) =>
        selected is not null && Read(providerId, selected.Name) is { Snoozed: true } state &&
        state.ResetAt == selected.ResetsAt?.ToUnixTimeSeconds();

    public AlertEvaluation Evaluate(UsageSnapshot snapshot, string providerName, string providerId)
    {
        var notifications = new List<UsageNotification>();
        var changed = false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var window in snapshot.Windows)
        {
            var key = Key(providerId, window.Name);
            var previous = Read(providerId, window.Name);
            var resetAt = window.ResetsAt?.ToUnixTimeSeconds();
            var level = AlertPolicy.Level(window.UsedPercent,
                settings.WarningThreshold, settings.CriticalThreshold);

            // A changed predicted reset time alone does not constitute a new cycle.
            // The old boundary must have elapsed, or usage must have fallen below
            // the warning threshold. Minor server time corrections preserve snoozing.
            var oldResetElapsed = previous?.ResetAt is long old &&
                now >= old && resetAt is long updated && updated > old;
            var usageDropped = previous is { Level: > 0 } && level == 0;
            var newCycle = previous is not null && (oldResetElapsed || usageDropped);

            if (settings.AlertsEnabled && newCycle && previous!.Level > 0 &&
                (oldResetElapsed || resetAt != previous.ResetAt))
                notifications.Add(new UsageNotification($"{providerName} limit reset",
                    $"{window.Name} usage window has reset.", UsageAlertSeverity.Info));

            if (settings.AlertsEnabled &&
                AlertPolicy.ShouldNotify(level, newCycle ? null : previous))
                notifications.Add(new UsageNotification($"{providerName} usage alert",
                    $"{window.Name} limit is {window.UsedPercent:0}% used.", UsageAlertSeverity.Warning));

            var recordedLevel = newCycle ? level : Math.Max(previous?.Level ?? 0, level);
            var snoozed = !newCycle && previous?.Snoozed == true;
            var next = new WindowAlertState(resetAt, recordedLevel, snoozed);
            if (!Equals(previous, next) || !settings.AlertStates.ContainsKey(key))
            {
                settings.AlertStates[key] = next;
                changed = true;
            }
        }
        return new AlertEvaluation(changed, notifications);
    }
}
