using CodexMonitor.Core.Workspaces;

namespace CodexMonitor.Infrastructure.Projects;

public sealed class WorkspaceService(
    MonitorSettings settings,
    IMonitorSettingsRepository settingsRepository,
    IAppPaths appPaths,
    ProjectCatalog catalog) : IWorkspaceService
{
    public event Action? Changed;

    public string SettingsPath => Path.Combine(appPaths.DataDirectory, "settings.json");
    public IReadOnlyList<ProjectDescriptor> Projects => catalog.Load(settings.ProjectPaths);
    public IReadOnlyList<ProjectGroup> Groups => ProjectGrouping.Group(Projects);
    public WorkspaceContext Current => new(ParseScope(settings.WorkspaceScope), settings.WorkspaceAgentId,
        Projects.FirstOrDefault(project => PathsEqual(project.Path, settings.WorkspaceProjectPath))?.Path);

    public string PreviewAddProject(string path)
    {
        var project = catalog.Add(path);
        if (Projects.Any(item => PathsEqual(item.Path, project.Path)))
            throw new InvalidOperationException("This project worktree is already in the project list.");
        return $"Target: {SettingsPath}\nAction: Add one project worktree to Codex Monitor settings.\nCurrent project folders: {settings.ProjectPaths.Count}\nAdd: {project.Path}\nRepository: {project.RepositoryRoot ?? "No Git repository"}\n\nNo project files will be changed.";
    }

    public void AddProject(string path)
    {
        var project = catalog.Add(path);
        var previous = settings.ProjectPaths.ToList();
        if (previous.Any(existing => PathsEqual(existing, project.Path))) return;
        settings.ProjectPaths = [.. previous, project.Path];
        PersistOrRestore(() => settings.ProjectPaths = previous);
    }

    public string PreviewRemoveProject(string path)
    {
        var project = Projects.FirstOrDefault(item => PathsEqual(item.Path, path))
            ?? throw new DirectoryNotFoundException("The selected project is no longer in the catalog.");
        var context = Current;
        var contextEffect = context.Scope == WorkspaceScope.Project && PathsEqual(context.ProjectPath, project.Path)
            ? "The active context will return to Global."
            : "The current context will remain unchanged.";
        return $"Target: {SettingsPath}\nAction: Remove this worktree from the monitor's saved project list.\nProject: {project.Path}\n{contextEffect}\nProject files will be kept.";
    }

    public void RemoveProject(string path)
    {
        var previousPaths = settings.ProjectPaths.ToList();
        var previousScope = settings.WorkspaceScope;
        var previousProject = settings.WorkspaceProjectPath;
        settings.ProjectPaths = previousPaths.Where(item => !PathsEqual(item, path)).ToList();
        if (settings.ProjectPaths.Count == previousPaths.Count) return;
        if (string.Equals(previousProject, path, StringComparison.OrdinalIgnoreCase))
        {
            settings.WorkspaceScope = nameof(WorkspaceScope.Global);
            settings.WorkspaceProjectPath = null;
        }
        PersistOrRestore(() =>
        {
            settings.ProjectPaths = previousPaths;
            settings.WorkspaceScope = previousScope;
            settings.WorkspaceProjectPath = previousProject;
        });
    }

    public string PreviewContext(WorkspaceContext context)
    {
        ValidateContext(context);
        var current = Current;
        return $"Target: {SettingsPath}\nAction: Save the selected workspace context.\nFrom: {current.Label}\nTo: {context.Label}\nProject path: {context.ProjectPath ?? "(none)"}\nAgent: {context.AgentId}";
    }

    public void SetContext(WorkspaceContext context)
    {
        ValidateContext(context);
        var previousScope = settings.WorkspaceScope;
        var previousAgent = settings.WorkspaceAgentId;
        var previousProject = settings.WorkspaceProjectPath;
        settings.WorkspaceScope = context.Scope.ToString();
        settings.WorkspaceAgentId = context.AgentId;
        settings.WorkspaceProjectPath = context.Scope == WorkspaceScope.Project ? context.ProjectPath : null;
        PersistOrRestore(() =>
        {
            settings.WorkspaceScope = previousScope;
            settings.WorkspaceAgentId = previousAgent;
            settings.WorkspaceProjectPath = previousProject;
        });
    }

    private void ValidateContext(WorkspaceContext context)
    {
        if (context.AgentId is not ("codex" or "claude" or "gemini"))
            throw new ArgumentException("Choose Codex, Claude, or Gemini.", nameof(context));
        if (context.Scope == WorkspaceScope.Project &&
            (context.ProjectPath is null || !Projects.Any(project => PathsEqual(project.Path, context.ProjectPath))))
            throw new InvalidOperationException("Choose a project that is present in the project list.");
    }

    private void PersistOrRestore(Action rollback)
    {
        try
        {
            settingsRepository.Save(settings);
            Changed?.Invoke();
        }
        catch
        {
            rollback();
            throw;
        }
    }

    private static WorkspaceScope ParseScope(string value) =>
        Enum.TryParse<WorkspaceScope>(value, ignoreCase: true, out var scope) ? scope : WorkspaceScope.Global;

    private static bool PathsEqual(string? left, string? right)
    {
        if (left is null || right is null) return left is null && right is null;
        try { return string.Equals(PathIdentity.Normalize(left), PathIdentity.Normalize(right), StringComparison.OrdinalIgnoreCase); }
        catch (ArgumentException) { return false; }
    }
}
