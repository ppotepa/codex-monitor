
namespace CodexMonitor.Plugins.Codex;

internal sealed class CodexCliService(IExecutableLocator locator, IProcessRunner runner)
{
    private const string PackageSpec = "codex-cli-usage==0.4.1";
    // Keep the fallback reproducible. The tag currently resolves to this commit;
    // an explicit hash avoids silently changing the installed tool later.
    private const string GitSource = "git+https://github.com/wakamex/clanker-usage.git@ace1cc1ac5e20c5c26c3d68a87c02415235c704a";

    private string? FindExecutable(string name) => locator.Find([name]);

    public async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var wrapper = FindExecutable("codex-cli-usage.exe");
        var core = FindExecutable("clanker-usage.exe");

        if (wrapper is not null)
        {
            try { return await ReadFromAsync(wrapper, ["json", "--max-age", "300"], cancellationToken); }
            catch (Exception ex) when (core is not null && CanTryFallback(ex))
            {
                return await ReadFromAsync(core, ["codex", "json", "--max-age", "300"], cancellationToken);
            }
        }
        if (core is not null)
            return await ReadFromAsync(core, ["codex", "json", "--max-age", "300"], cancellationToken);
        throw new PluginDependencyException(DependencyProblem.Missing, "Codex usage CLI is not installed.");
    }

    private async Task<UsageSnapshot> ReadFromAsync(string executable, string[] arguments, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try { result = await runner.RunAsync(executable, arguments, TimeSpan.FromSeconds(25), cancellationToken); }
        catch (ProcessLaunchException ex)
        {
            throw new PluginDependencyException(DependencyProblem.Broken, ex.Message);
        }
        catch (TimeoutException ex)
        {
            throw new CodexCliReadException(ex.Message, true, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CodexCliReadException(ex.Message, true, ex);
        }
        if (result.ExitCode != 0)
        {
            var error = ShortError(result.StandardError, result.StandardOutput);
            if (IsBrokenLauncherError(error))
                throw new PluginDependencyException(DependencyProblem.Broken, "The usage CLI cannot start because its Python interpreter is missing.");
            throw new CodexCliReadException(error, !IsAuthenticationError(error));
        }
        try { return CodexUsageParser.ParseLimits(result.StandardOutput); }
        catch (FormatException ex)
        {
            throw new CodexCliReadException("The usage CLI returned an unsupported response format.", true, ex);
        }
    }

    internal static bool IsBrokenLauncherError(string message) =>
        message.Contains("did not find executable at", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("fatal error in launcher", StringComparison.OrdinalIgnoreCase);

    public async Task InstallOrRepairAsync(CancellationToken cancellationToken)
    {
        var uv = FindExecutable("uv.exe") ?? throw new FileNotFoundException(
            "uv.exe was not found. Install uv from docs.astral.sh/uv, then try again.");

        var primary = await runner.RunAsync(uv, ["tool", "install", "--force", "--python", "3.12", PackageSpec],
            TimeSpan.FromMinutes(5), cancellationToken);
        if (primary.ExitCode == 0 && await IsCommandHealthyAsync("codex-cli-usage.exe", cancellationToken)) return;

        // The upstream Git repository contains the core command, while the PyPI package
        // provides the codex-cli-usage wrapper. The core accepts `codex json`.
        var fallback = await runner.RunAsync(uv, ["tool", "install", "--force", "--python", "3.12", GitSource],
            TimeSpan.FromMinutes(5), cancellationToken);
        if (fallback.ExitCode == 0 && await IsCommandHealthyAsync("clanker-usage.exe", cancellationToken)) return;

        var details = ShortError(fallback.StandardError, fallback.StandardOutput);
        throw new InvalidOperationException($"CLI installation failed. {details}");
    }

    private async Task<bool> IsCommandHealthyAsync(string name, CancellationToken cancellationToken)
    {
        var executable = FindExecutable(name);
        if (executable is null) return false;
        try
        {
            var result = await runner.RunAsync(executable, ["--version"], TimeSpan.FromSeconds(15), cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is PluginDependencyException or ProcessLaunchException or TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool CanTryFallback(Exception exception) => exception switch
    {
        OperationCanceledException => false,
        PluginDependencyException => true,
        CodexCliReadException read => read.CanFallback,
        _ => false
    };

    private static bool IsAuthenticationError(string message) =>
        message.Contains("auth", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("sign in", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("login", StringComparison.OrdinalIgnoreCase);

    private static string ShortError(string stderr, string stdout)
    {
        var message = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
        message = message.Trim();
        return message.Length > 240 ? message[..240] + "…" : message.Length > 0 ? message : "The command failed.";
    }
}

internal sealed class CodexCliReadException(string message, bool canFallback, Exception? inner = null)
    : Exception(message, inner)
{
    public bool CanFallback { get; } = canFallback;
}

