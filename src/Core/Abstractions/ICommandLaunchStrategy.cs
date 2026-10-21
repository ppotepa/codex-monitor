using System.Diagnostics;

namespace CodexMonitor.Core.Abstractions;

// Encapsulates platform-specific process wrappers such as Windows npm .cmd.
public interface ICommandLaunchStrategy
{
    ProcessStartInfo Create(string executable, IReadOnlyList<string> arguments, bool redirectStandardInput);
}
