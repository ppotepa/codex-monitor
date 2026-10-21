namespace CodexMonitor;

// State management is separate from refresh, WinForms, Avalonia and provider parsers.
internal sealed record UsageNotification(string Title, string Message, UsageAlertSeverity Severity);
internal sealed record AlertEvaluation(bool StateChanged, IReadOnlyList<UsageNotification> Notifications);

internal sealed class UsageAlertService(MonitorSettings settings)
{
    private static string Key(string pluginId, string windowName) => $"{pluginId}:{windowName}";

    private WindowAlertState? Read(string pluginId, string windowName) =>
        settings.AlertStates.TryGetValue(Key(pluginId, windowName), out var state) ? state : null;

    public void ToggleSnooze(UsageWindow? selected, string pluginId)
    {
        if (selected?.ResetsAt is null) return;
        var resetAt = selected.ResetsAt.Value.ToUnixTimeSeconds();
        var previous = Read(pluginId, selected.Name);
        var level = AlertPolicy.Level(selected.UsedPercent,
            settings.WarningThreshold, settings.CriticalThreshold);
        settings.AlertStates[Key(pluginId, selected.Name)] =
            new WindowAlertState(resetAt, Math.Max(previous?.Level ?? 0, level),
                !(previous?.Snoozed ?? false));
    }

    public bool IsSnoozed(UsageWindow? selected, string pluginId) =>
        selected is not null && Read(pluginId, selected.Name) is { Snoozed: true } state &&
        state.ResetAt == selected.ResetsAt?.ToUnixTimeSeconds();

    public AlertEvaluation Evaluate(UsageSnapshot snapshot, string pluginName, string pluginId)
    {
        var notifications = new List<UsageNotification>();
        var changed = false;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var window in snapshot.Windows)
        {
            var key = Key(pluginId, window.Name);
            var previous = Read(pluginId, window.Name);
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
                notifications.Add(new UsageNotification($"{pluginName} limit reset",
                    $"{window.Name} usage window has reset.", UsageAlertSeverity.Info));

            if (settings.AlertsEnabled &&
                AlertPolicy.ShouldNotify(level, newCycle ? null : previous))
                notifications.Add(new UsageNotification($"{pluginName} usage alert",
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
