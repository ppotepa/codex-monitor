using Microsoft.Win32;
using System.Runtime.Versioning;

namespace CodexMonitor.Infrastructure.Windows;

[SupportedOSPlatform("windows")]
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodexMonitor";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, QuotedExecutable, StringComparison.OrdinalIgnoreCase);
        }
        set
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? throw new InvalidOperationException("Cannot open Windows startup settings.");
            if (value) key.SetValue(ValueName, QuotedExecutable);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string QuotedExecutable => $"\"{Environment.ProcessPath}\"";

    public static bool IsPackagedExecutable => string.Equals(
        Path.GetFileName(Environment.ProcessPath), "CodexMonitor.exe", StringComparison.OrdinalIgnoreCase);
}
