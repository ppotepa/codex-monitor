using System.ComponentModel;
using System.Diagnostics;

namespace CodexMonitor;

internal enum CliProblem { None, Missing, Broken }

internal sealed class CliUnavailableException(CliProblem problem, string message) : Exception(message)
{
    public CliProblem Problem { get; } = problem;
}

internal static class CliService
{
    private const string GitSource = "git+https://github.com/wakamex/clanker-usage.git@v0.4.1";

    public static string? FindExecutable(string name)
    {
        var candidates = new List<string>();
        var localBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin");
        candidates.Add(Path.Combine(localBin, name));
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try { candidates.Add(Path.Combine(directory.Trim('"'), name)); }
            catch (ArgumentException) { }
        }
        return candidates.FirstOrDefault(File.Exists);
    }

    public static async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var wrapper = FindExecutable("codex-cli-usage.exe");
        var core = FindExecutable("clanker-usage.exe");

        if (wrapper is not null)
        {
            try { return await ReadFromAsync(wrapper, ["json", "--max-age", "300"], cancellationToken); }
            catch (CliUnavailableException) when (core is not null)
            {
                return await ReadFromAsync(core, ["codex", "json", "--max-age", "300"], cancellationToken);
            }
        }
        if (core is not null)
            return await ReadFromAsync(core, ["codex", "json", "--max-age", "300"], cancellationToken);
        throw new CliUnavailableException(CliProblem.Missing, "Brak narzędzia odczytującego limity Codex.");
    }

    private static async Task<UsageSnapshot> ReadFromAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        var result = await RunAsync(executable, arguments, TimeSpan.FromSeconds(25), cancellationToken);
        if (result.ExitCode != 0)
        {
            var error = ShortError(result.StandardError, result.StandardOutput);
            if (IsBrokenLauncherError(error))
                throw new CliUnavailableException(CliProblem.Broken, "Narzędzie CLI nie działa: brakuje jego interpretera Pythona.");
            throw new InvalidOperationException(error);
        }
        return UsageSnapshot.Parse(result.StandardOutput);
    }

    internal static bool IsBrokenLauncherError(string message) =>
        message.Contains("did not find executable at", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("fatal error in launcher", StringComparison.OrdinalIgnoreCase);

    public static async Task InstallOrRepairAsync(CancellationToken cancellationToken)
    {
        var uv = FindExecutable("uv.exe") ?? throw new FileNotFoundException(
            "Brak uv.exe. Zainstaluj uv z docs.astral.sh/uv, a potem ponów próbę.");

        var primary = await RunAsync(uv, ["tool", "install", "--force", "--python", "3.12", "codex-cli-usage"],
            TimeSpan.FromMinutes(5), cancellationToken);
        if (primary.ExitCode == 0 && await IsCommandHealthyAsync("codex-cli-usage.exe", cancellationToken)) return;

        // The upstream Git repository contains the core command, while the PyPI package
        // provides the codex-cli-usage wrapper. The core accepts `codex json`.
        var fallback = await RunAsync(uv, ["tool", "install", "--force", "--python", "3.12", GitSource],
            TimeSpan.FromMinutes(5), cancellationToken);
        if (fallback.ExitCode == 0 && await IsCommandHealthyAsync("clanker-usage.exe", cancellationToken)) return;

        var details = ShortError(fallback.StandardError, fallback.StandardOutput);
        throw new InvalidOperationException($"Instalacja CLI nie powiodła się. {details}");
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
        catch (CliUnavailableException) { return false; }
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
        catch (Win32Exception ex) { throw new CliUnavailableException(CliProblem.Broken, $"Nie można uruchomić {Path.GetFileName(executable)}: {ex.Message}"); }

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
            throw new TimeoutException($"{Path.GetFileName(executable)} nie odpowiedział w wyznaczonym czasie.");
        }
    }

    private static string ShortError(string stderr, string stdout)
    {
        var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        message = message.Trim();
        return message.Length > 240 ? message[..240] + "…" : message.Length > 0 ? message : "Polecenie zakończyło się błędem.";
    }
}
