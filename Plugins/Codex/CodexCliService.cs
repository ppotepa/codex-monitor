using System.ComponentModel;
using System.Diagnostics;
using CodexMonitor.Infrastructure.Windows;

namespace CodexMonitor.Plugins.Codex;

internal static class CodexCliService
{
    private const string GitSource = "git+https://github.com/wakamex/clanker-usage.git@v0.4.1";

    public static string? FindExecutable(string name)
        => WindowsExecutableLocator.Find([name]);

    public static async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var wrapper = FindExecutable("codex-cli-usage.exe");
        var core = FindExecutable("clanker-usage.exe");

        if (wrapper is not null)
        {
            try { return await ReadFromAsync(wrapper, ["json", "--max-age", "300"], cancellationToken); }
            catch (PluginDependencyException) when (core is not null)
            {
                return await ReadFromAsync(core, ["codex", "json", "--max-age", "300"], cancellationToken);
            }
        }
        if (core is not null)
            return await ReadFromAsync(core, ["codex", "json", "--max-age", "300"], cancellationToken);
        throw new PluginDependencyException(DependencyProblem.Missing, "Codex usage CLI is not installed.");
    }

    private static async Task<UsageSnapshot> ReadFromAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await RunAsync(executable, arguments, TimeSpan.FromSeconds(25), cancellationToken);
        if (result.ExitCode != 0)
        {
            var error = ShortError(result.StandardError, result.StandardOutput);
            if (IsBrokenLauncherError(error))
                throw new PluginDependencyException(DependencyProblem.Broken, "The usage CLI cannot start because its Python interpreter is missing.");
            throw new InvalidOperationException(error);
        }
        return CodexUsageParser.ParseLimits(result.StandardOutput);
    }

    internal static bool IsBrokenLauncherError(string message) =>
        message.Contains("did not find executable at", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("fatal error in launcher", StringComparison.OrdinalIgnoreCase);

    public static async Task InstallOrRepairAsync(CancellationToken cancellationToken)
    {
        var uv = FindExecutable("uv.exe") ?? throw new FileNotFoundException(
            "uv.exe was not found. Install uv from docs.astral.sh/uv, then try again.");

        var primary = await RunAsync(uv, ["tool", "install", "--force", "--python", "3.12", "codex-cli-usage"],
            TimeSpan.FromMinutes(5), cancellationToken);
        if (primary.ExitCode == 0 && await IsCommandHealthyAsync("codex-cli-usage.exe", cancellationToken)) return;

        // The upstream Git repository contains the core command, while the PyPI package
        // provides the codex-cli-usage wrapper. The core accepts `codex json`.
        var fallback = await RunAsync(uv, ["tool", "install", "--force", "--python", "3.12", GitSource],
            TimeSpan.FromMinutes(5), cancellationToken);
        if (fallback.ExitCode == 0 && await IsCommandHealthyAsync("clanker-usage.exe", cancellationToken)) return;

        var details = ShortError(fallback.StandardError, fallback.StandardOutput);
        throw new InvalidOperationException($"CLI installation failed. {details}");
    }

    private static async Task<bool> IsCommandHealthyAsync(string name, CancellationToken cancellationToken)
    {
        var executable = FindExecutable(name);
        if (executable is null) return false;
        try
        {
            var result = await RunAsync(executable, ["--version"], TimeSpan.FromSeconds(15), cancellationToken);
            return result.ExitCode == 0;
        }
        catch (PluginDependencyException) { return false; }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAsync(
        string executable, string[] arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try { process.Start(); }
        catch (Win32Exception ex) { throw new PluginDependencyException(DependencyProblem.Broken, $"Cannot start {Path.GetFileName(executable)}: {ex.Message}"); }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var outputTask = process.StandardOutput.ReadToEndAsync(timeoutSource.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return (process.ExitCode, await outputTask, await errorTask);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException($"{Path.GetFileName(executable)} did not respond in time.");
        }
    }

    private static string ShortError(string stderr, string stdout)
    {
        var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        message = message.Trim();
        return message.Length > 240 ? message[..240] + "…" : message.Length > 0 ? message : "The command failed.";
    }
}
