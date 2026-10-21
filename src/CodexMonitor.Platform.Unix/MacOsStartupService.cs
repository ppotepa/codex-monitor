using System.Security;

namespace CodexMonitor.Platform.Unix;

// A LaunchAgent starts the published menu-bar host at user login.
internal sealed class MacOsStartupService : IStartupService
{
    private readonly string _plistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "io.github.ppotepa.codex-monitor.plist");

    public string MenuLabel => "Start at login";
    public bool CanConfigure => PublishedExecutable is not null;
    public bool Enabled
    {
        get => File.Exists(_plistPath);
        set
        {
            if (!value) { if (File.Exists(_plistPath)) File.Delete(_plistPath); return; }
            var executable = PublishedExecutable ??
                throw new InvalidOperationException("Publish Codex Monitor as a native executable to configure startup.");
            var escaped = SecurityElement.Escape(executable) ??
                throw new InvalidOperationException("Invalid executable path.");
            Directory.CreateDirectory(Path.GetDirectoryName(_plistPath)!);
            File.WriteAllText(_plistPath,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " +
                "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                "<plist version=\"1.0\"><dict>" +
                "<key>Label</key><string>io.github.ppotepa.codex-monitor</string>" +
                "<key>ProgramArguments</key><array><string>" + escaped + "</string></array>" +
                "<key>RunAtLoad</key><true/>" +
                "</dict></plist>\n");
        }
    }

    private static string? PublishedExecutable =>
        Environment.ProcessPath is { } path &&
        !string.Equals(Path.GetFileNameWithoutExtension(path), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? path : null;
}
