using System.Globalization;
using System.Text.Json;

namespace CodexMonitor;

internal sealed record UsageWindow(string Name, double UsedPercent, DateTimeOffset? ResetsAt);

internal sealed record UsageSnapshot(string? Plan, DateTimeOffset? RetrievedAt, IReadOnlyList<UsageWindow> Windows)
{
    public string? Status { get; init; }

    public UsageWindow? MainWindow => Windows.FirstOrDefault(w => w.Name.Equals("5-hour", StringComparison.OrdinalIgnoreCase))
        ?? Windows.FirstOrDefault(w => w.Name.Equals("weekly", StringComparison.OrdinalIgnoreCase))
        ?? Windows.FirstOrDefault();

    public static UsageSnapshot Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schema", out var schema) || schema.GetString() != "usage-v1" ||
            !root.TryGetProperty("provider", out var provider) || provider.GetString() != "codex")
            throw new FormatException("Nieznany format odpowiedzi codex-cli-usage.");

        if (!root.TryGetProperty("windows", out var windowsElement) || windowsElement.ValueKind != JsonValueKind.Array)
            throw new FormatException("Odpowiedź nie zawiera listy limitów.");

        var windows = new List<UsageWindow>();
        foreach (var item in windowsElement.EnumerateArray())
        {
            if (!item.TryGetProperty("used_pct", out var pctElement) || pctElement.ValueKind != JsonValueKind.Number ||
                !pctElement.TryGetDouble(out var pct))
                continue;

            var name = item.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? "Limit" : "Limit";
            windows.Add(new UsageWindow(name, Math.Clamp(pct, 0, 100), ReadDate(item, "resets_at")));
        }

        if (windows.Count == 0)
            throw new FormatException("Odpowiedź nie zawiera dostępnego limitu.");

        var plan = root.TryGetProperty("plan", out var planElement) && planElement.ValueKind == JsonValueKind.String
            ? planElement.GetString() : null;
        var status = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString() : null;
        return new UsageSnapshot(plan, ReadDate(root, "retrieved_at"), windows) { Status = status };
    }

    private static DateTimeOffset? ReadDate(JsonElement parent, string key)
    {
        if (parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            return date;
        return null;
    }
}
