namespace CodexMonitor.Core.Notifications.Alerts;

internal static class AlertPolicy
{
    public static int Level(double usedPercent, int warning, int critical) =>
        usedPercent >= critical ? 2 : usedPercent >= warning ? 1 : 0;

    // A new cycle is represented explicitly by a missing previous state.
    // A changed reset estimate alone never authorizes a repeat warning.
    public static bool ShouldNotify(int level, WindowAlertState? previous) =>
        level > 0 && (previous is null || (!previous.Snoozed && level > previous.Level));
}
