namespace CodexMonitor.Platform.Unix;

// XDG autostart entry for a published desktop executable (not dotnet run).
internal sealed class LinuxStartupService : IStartupService
{
    private readonly string _entryPath = Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
            ? xdg : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config"),
        "autostart", "codex-monitor.desktop");

    public string MenuLabel => "Start with Linux";
    public bool CanConfigure => PublishedExecutable is not null;
    public bool Enabled
    {
        get => File.Exists(_entryPath);
        set
        {
            if (!value) { if (File.Exists(_entryPath)) File.Delete(_entryPath); return; }
            var executable = PublishedExecutable ??
                throw new InvalidOperationException("Publish Codex Monitor as a native executable to configure startup.");
            if (executable.Contains('\n') || executable.Contains('\r'))
                throw new InvalidOperationException("Unexpected newline in executable path.");
            var escaped = executable.Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("$", "\\$").Replace("`", "\\`");
            Directory.CreateDirectory(Path.GetDirectoryName(_entryPath)!);
            File.WriteAllText(_entryPath,
                "[Desktop Entry]\nType=Application\nName=Codex Monitor\n" +
                $"Exec=\"{escaped}\"\nTerminal=false\nX-GNOME-Autostart-enabled=true\n");
        }
    }

    private static string? PublishedExecutable =>
        Environment.ProcessPath is { } path &&
        !string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? path : null;
}
