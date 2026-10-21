namespace CodexMonitor.Platform.Unix;

// Linux and macOS share the PATH lookup; UI and startup remain host-specific.
internal sealed class UnixExecutableLocator : IExecutableLocator
{
    public string? Find(IEnumerable<string> executableNames)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var searchDirectories = new[]
        {
            Path.Combine(home, ".local", "bin"),
            Path.Combine(home, ".cargo", "bin"),
            Path.Combine(home, ".npm-global", "bin"),
            "/usr/local/bin",
            "/opt/homebrew/bin"
        }.Concat((Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            .Where(Directory.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var original in executableNames)
        {
            var name = original.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                       original.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(original) : original;
            if (Path.IsPathFullyQualified(name) && IsExecutable(name)) return name;
            foreach (var directory in searchDirectories)
            {
                var candidate = Path.Combine(directory, name);
                if (IsExecutable(candidate)) return candidate;
            }
        }
        return null;
    }

    private static bool IsExecutable(string path)
    {
        if (!File.Exists(path)) return false;
        if (OperatingSystem.IsWindows()) return true;
        try
        {
            var mode = File.GetUnixFileMode(path);
            return mode.HasFlag(UnixFileMode.UserExecute) ||
                   mode.HasFlag(UnixFileMode.GroupExecute) ||
                   mode.HasFlag(UnixFileMode.OtherExecute);
        }
        catch (PlatformNotSupportedException)
        {
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
