using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;

namespace CodexMonitor.Plugins.Codex;

internal sealed class CodexTokenClient(IExecutableLocator locator, ICommandLaunchStrategy launchStrategy)
{
    private const int TimeoutSeconds = 24;
    private static long _nextRequestId;

    public async Task<TokenUsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var launcher = locator.Find(["codex.exe", "codex", "codex.cmd"]) ??
            throw new InvalidOperationException("Codex CLI was not found. Install or update Codex CLI, then refresh.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        using var process = new Process
        {
            StartInfo = launchStrategy.Create(launcher, ["app-server", "--listen", "stdio://"], redirectStandardInput: true)
        };

        try { process.Start(); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException("Could not start the local Codex app-server.", ex);
        }
        _ = process.StandardError.ReadToEndAsync();

        var waitingFor = "app-server initialization";
        try
        {
            await SendAsync(process, new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "codex_monitor", title = "Codex Monitor", version = "0.4.0" } } }, timeout.Token);
            var initialized = await ReadResponseAsync(process, 1, timeout.Token);
            EnsureSuccess(initialized);
            await SendAsync(process, new { method = "initialized", @params = new { } }, timeout.Token);

            var requestId = Interlocked.Increment(ref _nextRequestId) + 1;
            // Codex's canonical wire format omits params for account/usage/read.
            waitingFor = "account token activity";
            await SendAsync(process, new { id = requestId, method = "account/usage/read" }, timeout.Token);
            var response = await ReadResponseAsync(process, requestId, timeout.Token);
            EnsureSuccess(response);
            return CodexTokenParser.ParseTokens(response.GetProperty("result").GetRawText(), DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Codex {waitingFor} did not respond in time.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Codex returned an unreadable account activity response.", ex);
        }
        finally
        {
            try { process.StandardInput.Close(); } catch (Exception ex) when (ex is InvalidOperationException or IOException) { }
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
                using var cleanupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await process.WaitForExitAsync(cleanupDeadline.Token); }
                catch (OperationCanceledException) { /* Kill can fail; never hang on cleanup. */ }
            }
        }
    }

    private static async Task SendAsync(Process process, object message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        await process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<JsonElement> ReadResponseAsync(Process process, long requestId, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null) throw new InvalidOperationException("Codex app-server closed before returning account activity.");
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var value) && value == requestId)
                    return root.Clone();
            }
            catch (JsonException)
            {
                // Ignore non-protocol output from local CLI startup wrappers.
            }
        }
    }

    private static void EnsureSuccess(JsonElement response)
    {
        if (!response.TryGetProperty("error", out var error)) return;
        var message = error.TryGetProperty("message", out var messageValue) ? messageValue.ToString() : "";
        var isAuthentication = message.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("sign in", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("login", StringComparison.OrdinalIgnoreCase);
        throw new InvalidOperationException(isAuthentication
            ? "Sign in to Codex CLI, then refresh token activity."
            : "Codex could not read account token activity. Check the Codex CLI version and sign-in status.");
    }
}
