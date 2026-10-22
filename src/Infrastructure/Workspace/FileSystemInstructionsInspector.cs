using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

// Read-only inspector. Does not follow directory junctions/symlinks or invoke an agent.
public sealed class FileSystemInstructionsInspector : IWorkspaceInstructionsInspector
{
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
    { ".git", "node_modules", "bin", "obj", ".venv", "dist", "target" };

    public IReadOnlyList<WorkspaceInstructionFile> Find(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var found = new List<WorkspaceInstructionFile>();
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = 0;
        while (queue.Count > 0 && visited++ < 3000 && found.Count < 200)
        {
            var (folder, depth) = queue.Dequeue();
            foreach (var name in new[] { "AGENTS.md", "AGENTS.override.md" })
            {
                var path = Path.Combine(folder, name);
                if (!File.Exists(path)) continue;
                var info = new FileInfo(path);
                if ((info.Attributes & FileAttributes.ReparsePoint) == 0 && info.Length <= 1024 * 1024)
                    found.Add(new WorkspaceInstructionFile(path, info.Length));
            }
            if (depth >= 6) continue;
            foreach (var directory in Directory.EnumerateDirectories(folder))
            {
                var info = new DirectoryInfo(directory);
                if (Excluded.Contains(info.Name)) continue;
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                queue.Enqueue((directory, depth + 1));
            }
        }
        return found.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public string Read(string filePath)
    {
        var info = new FileInfo(filePath);
        if (!info.Exists || info.Length > 1024 * 1024 ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Instruction file is missing or exceeds 1 MB.");
        if (info.Name is not ("AGENTS.md" or "AGENTS.override.md"))
            throw new InvalidOperationException("Only recognized instruction files can be inspected.");
        return File.ReadAllText(info.FullName);
    }
}
