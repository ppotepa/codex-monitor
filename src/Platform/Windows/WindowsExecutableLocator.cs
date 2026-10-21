namespace CodexMonitor.Platform.Windows;

// Windows executable discovery; the list of agents comes from Core provider metadata.
internal sealed class WindowsExecutableLocator : IExecutableLocator
{
    public string? Find(IEnumerable<string> names)
    {
        var directories = SearchDirectories();
        foreach (var name in names)
        {
            foreach (var directory in directories)
            {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    private static IReadOnlyList<string> SearchDirectories()
    {
        var result = new List<string>();
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        result.Add(Path.Combine(user, ".local", "bin"));
        result.Add(Path.Combine(user, ".cargo", "bin"));
        result.Add(Path.Combine(appData, "npm"));
        result.Add(Path.Combine(localAppData, "Programs"));
        result.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => directory.Trim('"')));
        return result.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
