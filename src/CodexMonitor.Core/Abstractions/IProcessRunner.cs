using System.ComponentModel;
using System.Diagnostics;

namespace CodexMonitor;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal sealed class ProcessLaunchException(string message, Exception innerException)
    : Exception(message, innerException);

// One-shot process API shared by providers. Interactive protocols use ICommandLaunchStrategy directly.
internal interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken);
}

internal sealed class ProcessRunner(ICommandLaunchStrategy commands) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = commands.Create(executable, arguments, redirectStandardInput: false)
        };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new ProcessLaunchException(
                $"Cannot start {Path.GetFileName(executable)}: {ex.Message}", ex);
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var error = process.StandardError.ReadToEndAsync(deadline.Token);

        try
        {
            await process.WaitForExitAsync(deadline.Token);
            return new ProcessResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception) { }
            }
            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException($"{Path.GetFileName(executable)} did not respond in time.");
        }
    }
}
