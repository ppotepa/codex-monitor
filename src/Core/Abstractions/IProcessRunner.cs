namespace CodexMonitor.Core.Abstractions;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class ProcessLaunchException(string message, Exception innerException)
    : Exception(message, innerException);

// One-shot process API shared by providers. Interactive protocols use ICommandLaunchStrategy directly.
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken);
}
