namespace CodexMonitor.Core.Workspaces;

public enum WorkspaceScope
{
    Global,
    Agent,
    Project
}

public enum AvailabilityStatus
{
    NotImplemented,
    NotInstalled,
    Unavailable,
    PermissionRequired,
    NoData,
    StaleData,
    Ready
}

public sealed record WorkspaceContext(WorkspaceScope Scope, string AgentId, string? ProjectPath)
{
    public string Label => Scope switch
    {
        WorkspaceScope.Agent => $"Agent · {AgentId}",
        WorkspaceScope.Project => $"Project · {Path.GetFileName(ProjectPath) ?? "Choose project"}",
        _ => "Global"
    };
}

public sealed record ProjectDescriptor(string Path, string Name, string? RepositoryRoot, string? CommonGitDirectory)
{
    public bool IsGitRepository => CommonGitDirectory is not null;
    public string RepositoryKey => CommonGitDirectory is null
        ? $"folder:{PathIdentity.Normalize(Path)}"
        : $"git:{PathIdentity.Normalize(CommonGitDirectory)}";
}

public sealed record ProjectGroup(string Name, string RepositoryRoot, IReadOnlyList<ProjectDescriptor> Worktrees);

public static class ProjectGrouping
{
    public static IReadOnlyList<ProjectGroup> Group(IEnumerable<ProjectDescriptor> projects) =>
        projects.GroupBy(project => project.RepositoryKey, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var worktrees = group.OrderBy(project => project.Path, StringComparer.OrdinalIgnoreCase).ToArray();
                var root = worktrees.Select(project => project.RepositoryRoot)
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? worktrees[0].Path;
                var name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return new ProjectGroup(string.IsNullOrWhiteSpace(name) ? root : name, root, worktrees);
            })
            .OrderBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public static class PathIdentity
{
    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (!string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? full.ToUpperInvariant() : full;
    }
}
