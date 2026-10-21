using System.Diagnostics;

namespace CodexMonitor.Infrastructure.Windows;

internal sealed class WindowsCommandLaunchStrategy : ICommandLaunchStrategy
{
    public ProcessStartInfo Create(string executable, IReadOnlyList<string> arguments, bool redirectStandardInput)
    {
        var isCommandScript = executable.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                              executable.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var commandProcessor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var info = new ProcessStartInfo(isCommandScript ? commandProcessor : executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (isCommandScript)
        {
            info.ArgumentList.Add("/d");
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add(executable);
        }
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return info;
    }
}
