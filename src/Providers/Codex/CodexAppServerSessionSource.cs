using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Providers.Codex;

// Experimental and read-only. Starts its own app-server; never claims to observe all live hosts.
public sealed class CodexAppServerSessionSource(IExecutableLocator locator, ICommandLaunchStrategy commands)
    : IAgentSessionSource
{
    public string ProviderId => "codex";
    public string FeatureId => "sessions.list";
    public int Priority => 10;

    public async Task<AgentSessionSnapshot> ListAsync(CancellationToken cancellationToken)
    {
        var executable = locator.Find(["codex", "codex.exe", "codex.cmd"])
            ?? throw new NotSupportedException("Codex CLI is not installed.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var process = new Process
        {
            StartInfo = commands.Create(executable, ["app-server"], redirectStandardInput: true)
        };
        try
        {
            process.Start();
            process.StandardInput.AutoFlush = true;
            // Drain stderr so the child cannot block when it writes diagnostics.
            var drain = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteLineAsync(
                """{"id":1,"method":"initialize","params":{"clientInfo":{"name":"codex_monitor","title":"CodexMonitor","version":"0.5.3"},"capabilities":{}}}"""
                .AsMemory(), timeout.Token);
            await ReadResultAsync(process.StandardOutput, 1, timeout.Token);
            await process.StandardInput.WriteLineAsync(
                """{"method":"initialized","params":{}}""".AsMemory(), timeout.Token);
            await process.StandardInput.WriteLineAsync(
                """{"id":2,"method":"thread/list","params":{"limit":50,"sortKey":"recency_at","sourceKinds":["cli","vscode","appServer","exec","subAgent"]}}"""
                .AsMemory(), timeout.Token);
            var result = await ReadResultAsync(process.StandardOutput, 2, timeout.Token);
            var sessions = new List<AgentSessionInfo>();
            if (result.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var thread in data.EnumerateArray())
                {
                    if (thread.ValueKind != JsonValueKind.Object) continue;
                    var id = String(thread, "id");
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var status = "Unknown";
                    if (thread.TryGetProperty("status", out var state) &&
                        state.ValueKind == JsonValueKind.Object)
                    {
                        status = String(state, "type") ?? "Unknown";
                        if (status == "active" && state.TryGetProperty("activeFlags", out var flags) &&
                            flags.ValueKind == JsonValueKind.Array &&
                            flags.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String &&
                                value.GetString() == "waitingOnApproval"))
                            status = "waitingOnApproval";
                    }
                    // Do not display preview/user prompts as a fallback name.
                    var name = String(thread, "name") ?? "Unnamed session";
                    var cwd = String(thread, "cwd");
                    DateTimeOffset? updated = null;
                    if (thread.TryGetProperty("updatedAt", out var timestamp) &&
                        timestamp.ValueKind == JsonValueKind.Number &&
                        timestamp.TryGetInt64(out var seconds) && seconds is > 0 and < 253402300799)
                        updated = DateTimeOffset.FromUnixTimeSeconds(seconds);
                    sessions.Add(new AgentSessionInfo(id!, "codex", cwd, name, status, updated));
                }
            }
            return new(sessions, "Read-only stored sessions from a separate App Server instance. " +
                "External active state may be notLoaded or unknown; no sessions were resumed.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Cannot launch Codex app-server.", ex);
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
        }
    }

    private static string? String(JsonElement obj, string key)
        => obj.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static async Task<JsonElement> ReadResultAsync(StreamReader reader, int expectedId,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 200; i++)
        {
            var line = await reader.ReadLineAsync(cancellationToken)
                ?? throw new IOException("Codex app-server closed before answering.");
            if (line.Length > 512_000) throw new InvalidDataException("App Server response exceeds 512 KB.");
            JsonDocument document;
            try { document = JsonDocument.Parse(line); }
            catch (JsonException) { continue; }
            using (document)
            {
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number ||
                    !id.TryGetInt32(out var value) || value != expectedId) continue;
                if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                    throw new InvalidOperationException("Codex app-server rejected the read-only request: " +
                        error.ToString()[..Math.Min(error.ToString().Length, 250)]);
                return root.GetProperty("result").Clone();
            }
        }
        throw new InvalidDataException("Too many unrelated App Server notifications.");
    }
}
