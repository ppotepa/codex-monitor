using System.Diagnostics;

namespace CodexMonitor.Platform.Unix;

internal sealed class UnixCommandLaunchStrategy : ICommandLaunchStrategy
{
    public ProcessStartInfo Create(string executable, IReadOnlyList<string> arguments, bool redirectStandardInput)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }
}
