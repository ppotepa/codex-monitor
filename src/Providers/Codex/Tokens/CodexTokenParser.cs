using System.Globalization;
using System.Text.Json;

namespace CodexMonitor.Providers.Codex.Tokens;

internal static class CodexTokenParser
{
    public static TokenUsageSnapshot ParseTokens(string json, DateTimeOffset retrievedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var summary = root.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.Object
            ? summaryElement : throw new FormatException("Token usage response has no summary.");

        var daily = new List<DailyTokenUsage>();
        if (root.TryGetProperty("dailyUsageBuckets", out var buckets) && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (var bucket in buckets.EnumerateArray())
            {
                if (!bucket.TryGetProperty("startDate", out var dateValue) || dateValue.ValueKind != JsonValueKind.String ||
                    !DateOnly.TryParse(dateValue.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                    !bucket.TryGetProperty("tokens", out var tokensValue) || tokensValue.ValueKind != JsonValueKind.Number || !tokensValue.TryGetInt64(out var tokens) || tokens < 0)
                    continue;
                daily.Add(new DailyTokenUsage(date, tokens));
            }
        }

        return new TokenUsageSnapshot(retrievedAt,
            ReadLong(summary, "lifetimeTokens"),
            ReadLong(summary, "peakDailyTokens"),
            ReadInt(summary, "currentStreakDays"),
            daily.GroupBy(x => x.Date).Select(group => group.Last()).OrderBy(x => x.Date).ToArray());
    }

    private static long? ReadLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number >= 0
            ? number : null;

    private static int? ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number >= 0
            ? number : null;
}

