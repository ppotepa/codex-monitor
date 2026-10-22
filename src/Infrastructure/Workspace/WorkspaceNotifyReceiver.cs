using System.Text.Json;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

// Invoked by Codex notify/hooks, before starting Avalonia or acquiring the desktop singleton.
// Explicit opt-in setup; only harmless identifiers and event names are stored.
public static class WorkspaceNotifyReceiver
{
    public static int Capture(string[] args, string dataDirectory)
    {
        try
        {
            var source = args[0] == "--codex-hook" ? "hook" : "notify";
            string json;
            if (source == "notify")
            {
                if (args.Length < 2) return 2;
                json = args[1];
            }
            else
            {
                var buffer = new char[65_537];
                var read = Console.In.ReadBlock(buffer, 0, buffer.Length);
                if (read >= buffer.Length) return 2;
                json = new string(buffer, 0, read);
            }
            if (json.Length is < 2 or > 65_536) return 2;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return 2;
            var eventType = Get(root, "hook_event_name") ?? Get(root, "type") ?? Get(root, "event") ?? "Unknown";
            var cwd = Get(root, "cwd") ?? Get(root, "workdir");
            var sessionId = Get(root, "thread-id") ?? Get(root, "thread_id") ?? Get(root, "session_id");
            // Only an expected event name can be shown as an observed activity type.
            if (eventType.Length > 80 || (cwd?.Length ?? 0) > 2048 ||
                (sessionId?.Length ?? 0) > 256) return 2;
            var item = new WorkspaceActivityEvent(DateTimeOffset.UtcNow, "codex", eventType,
                cwd is null ? null : Path.GetFullPath(cwd), sessionId, source);
            Directory.CreateDirectory(dataDirectory);
            var journal = Path.Combine(dataDirectory, "activity.jsonl");
            var mutexName = OperatingSystem.IsWindows() ? @"Local\CodexMonitorEventJournal" : "CodexMonitorEventJournal";
            using var gate = new Mutex(false, mutexName);
            var acquired = false;
            try
            {
                try { acquired = gate.WaitOne(TimeSpan.FromSeconds(3)); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired) return 3;
                if (File.Exists(journal) && new FileInfo(journal).Length > 4 * 1024 * 1024)
                    File.Move(journal, journal + ".old", overwrite: true);
                File.AppendAllText(journal, JsonSerializer.Serialize(item) + Environment.NewLine);
                return 0;
            }
            finally
            {
                if (acquired) gate.ReleaseMutex();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                   or ArgumentException or NotSupportedException)
        {
            return 3;
        }
    }

    private static string? Get(JsonElement value, string name)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return null;
        return property.GetString();
    }
}
