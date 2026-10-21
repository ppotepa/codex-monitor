namespace CodexMonitor;

internal static class AlertPolicy
{
    public static int Level(double usedPercent, int warning, int critical) =>
        usedPercent >= critical ? 2 : usedPercent >= warning ? 1 : 0;

    public static bool ShouldNotify(int level, WindowAlertState? previous, long? resetAt) =>
        level > 0 && (previous is null || previous.ResetAt != resetAt ||
            (!previous.Snoozed && level > previous.Level));
}
