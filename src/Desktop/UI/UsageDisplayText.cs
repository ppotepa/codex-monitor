using System.Globalization;

namespace CodexMonitor.UI;

// Shared labels and formatting used by the Avalonia presentation and tray host.
public static class UsageDisplayText
{
    public static string? CombineErrors(params string?[] errors)
    {
        var messages = errors.Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct().ToArray();
        return messages.Length == 0 ? null : string.Join(Environment.NewLine, messages);
    }

    public static string WindowName(string? name) => name?.ToLowerInvariant() switch
    {
        "5-hour" or "5h" => "5-hour limit",
        "weekly" => "Weekly limit",
        "daily" => "Daily limit",
        null => "Usage limit",
        _ => name
    };

    public static string FormatTokenCount(long? count) =>
        count is null ? "—" : count.Value.ToString("N0", CultureInfo.CurrentCulture);

    public static string UpdatedText(DateTimeOffset? at, bool stale) => at is { } date
        ? $"{(stale ? "Last successful" : "Updated")} {date.ToLocalTime():MMM d, HH:mm}"
        : "Waiting for usage data";

    public static string StatusText(ProviderHealth health) => health switch
    {
        ProviderHealth.Ready => "Ready",
        ProviderHealth.Refreshing => "Updating",
        ProviderHealth.Stale => "Stale data",
        ProviderHealth.Missing => "Setup needed",
        ProviderHealth.Broken => "Repair needed",
        ProviderHealth.Error => "Error",
        _ => "Waiting"
    };

    public static string TokenUpdatedText(DateTimeOffset? at, bool busy) => busy
        ? at is null ? "Loading account activity..." : $"Refreshing · updated {at.Value.ToLocalTime():HH:mm}"
        : at is { } date ? $"Account activity · updated {date.ToLocalTime():MMM d, HH:mm}"
        : "Account token activity";

    public static string ResetShort(DateTimeOffset? at) => at is null ? "Reset time unavailable" :
        at <= DateTimeOffset.Now ? "reset pending" : $"resets in {Duration(at.Value - DateTimeOffset.Now)}";

    public static string ResetText(DateTimeOffset? at) => at is null ? "Reset time unavailable" :
        at <= DateTimeOffset.Now ? "Reset pending" : $"Resets in {Duration(at.Value - DateTimeOffset.Now)}\n{at.Value.ToLocalTime():MMM d, HH:mm}";

    public static string Duration(TimeSpan left) => left.TotalDays >= 1 ? $"{(int)left.TotalDays}d {left.Hours}h" :
        left.TotalHours >= 1 ? $"{(int)left.TotalHours}h {left.Minutes}m" :
        $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}m";
}
