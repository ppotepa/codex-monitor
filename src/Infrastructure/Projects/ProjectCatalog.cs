using CodexMonitor.Core.Workspaces;

namespace CodexMonitor.Infrastructure.Projects;

public sealed class ProjectCatalog
{
    public IReadOnlyList<ProjectDescriptor> Load(IEnumerable<string> paths) => paths
        .Select(TryResolve)
        .Where(project => project is not null)
        .Cast<ProjectDescriptor>()
        .DistinctBy(project => PathIdentity.Normalize(project.Path), StringComparer.OrdinalIgnoreCase)
        .OrderBy(project => project.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public ProjectDescriptor Add(string path) => TryResolve(path)
        ?? throw new DirectoryNotFoundException($"Project folder was not found: {path}");

    public ProjectDescriptor? TryResolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException) { return null; }
        if (!Directory.Exists(fullPath)) return null;
        var root = FindWorktreeRoot(fullPath);
        if (root is null) return new ProjectDescriptor(fullPath, DirectoryName(fullPath), null, null);
        try
        {
            var (commonDirectory, repositoryRoot) = ResolveGitDirectories(root);
            return new ProjectDescriptor(root, DirectoryName(root), repositoryRoot, commonDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            return new ProjectDescriptor(root, DirectoryName(root), null, null);
        }
    }

    private static string? FindWorktreeRoot(string selectedPath)
    {
        for (var current = new DirectoryInfo(selectedPath); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
        return null;
    }

    private static (string CommonDirectory, string RepositoryRoot) ResolveGitDirectories(string worktree)
    {
        var gitMarker = Path.Combine(worktree, ".git");
        var gitDirectory = gitMarker;
        if (File.Exists(gitMarker))
        {
            var marker = File.ReadAllText(gitMarker).Trim();
            const string prefix = "gitdir:";
            if (!marker.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"Invalid .git worktree file in {worktree}");
            var gitPath = marker[prefix.Length..].Trim();
            gitDirectory = Path.GetFullPath(Path.IsPathRooted(gitPath) ? gitPath : Path.Combine(worktree, gitPath));
        }
        var commonFile = Path.Combine(gitDirectory, "commondir");
        var commonDirectory = File.Exists(commonFile)
            ? Path.GetFullPath(Path.Combine(gitDirectory, File.ReadAllText(commonFile).Trim()))
            : gitDirectory;
        var repositoryRoot = Directory.GetParent(commonDirectory)?.FullName ?? worktree;
        return (commonDirectory, repositoryRoot);
    }

    private static string DirectoryName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }
}
