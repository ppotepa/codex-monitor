using System.Diagnostics;

namespace CodexMonitor;

// Encapsulates platform-specific process wrappers such as Windows npm .cmd.
internal interface ICommandLaunchStrategy
{
    ProcessStartInfo Create(string executable, IReadOnlyList<string> arguments, bool redirectStandardInput);
}
