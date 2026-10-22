using System.Text.Json;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

// Reads only metadata from the application's own journal, never private transcript content.
public sealed class JsonlWorkspaceActivitySource(string journalPath) : IWorkspaceActivitySource
{
    public WorkspaceActivitySnapshot ReadRecent(string? projectDirectory, int maxEvents = 80)
    {
        maxEvents = Math.Clamp(maxEvents, 1, 200);
        if (!File.Exists(journalPath))
            return new([], false, "Not configured. Enable a Codex notify/hook to observe events.", null);
        var queue = new Queue<WorkspaceActivityEvent>(maxEvents);
        foreach (var line in File.ReadLines(journalPath))
        {
            if (line.Length > 16_384 || string.IsNullOrWhiteSpace(line)) continue;
            WorkspaceActivityEvent? item;
            try { item = JsonSerializer.Deserialize<WorkspaceActivityEvent>(line); }
            catch (JsonException) { continue; }
            if (item is null || item.ProviderId != "codex") continue;
            if (!string.IsNullOrWhiteSpace(projectDirectory) &&
                !string.Equals(item.ProjectDirectory, projectDirectory, StringComparison.OrdinalIgnoreCase))
                continue;
            queue.Enqueue(item);
            if (queue.Count > maxEvents) queue.Dequeue();
        }
        var ordered = queue.OrderByDescending(e => e.At).ToArray();
        return new(ordered, true,
            "Partial observations from explicitly configured local Codex events. Other hosts and active status may be unknown.",
            ordered.FirstOrDefault()?.At);
    }
}
