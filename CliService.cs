using System.ComponentModel;
using System.Diagnostics;

namespace CodexMonitor;

internal static class CliService
{
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
        var executable = FindExecutable("codex-cli-usage.exe")
            ?? throw new FileNotFoundException("Brak codex-cli-usage.exe.");
        var result = await RunAsync(executable, ["json", "--max-age", "300"], TimeSpan.FromSeconds(25), cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(ShortError(result.StandardError, result.StandardOutput));
        return UsageSnapshot.Parse(result.StandardOutput);
    }

    public static async Task InstallAsync(CancellationToken cancellationToken)
    {
        var uv = FindExecutable("uv.exe") ?? throw new FileNotFoundException("Brak uv.exe. Zainstaluj uv, a następnie uruchom: uv tool install codex-cli-usage");
        var result = await RunAsync(uv, ["tool", "install", "codex-cli-usage"], TimeSpan.FromMinutes(3), cancellationToken);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(ShortError(result.StandardError, result.StandardOutput));
        if (FindExecutable("codex-cli-usage.exe") is null)
            throw new FileNotFoundException("Instalacja zakończyła się, ale nie znaleziono codex-cli-usage.exe.");
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
        catch (Win32Exception ex) { throw new InvalidOperationException($"Nie można uruchomić {Path.GetFileName(executable)}: {ex.Message}", ex); }

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
