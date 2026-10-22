using System.Collections.ObjectModel;
using CodexMonitor.Core.Workspaces;

namespace CodexMonitor.UI.ViewModels;

public sealed record WorkspaceProjectChoice(string Name, string? Path);

public sealed class WorkspaceViewModel : BindableBase
{
    private readonly IWorkspaceService _workspace;
    private readonly IGitImpactService _gitImpact;
    private string _page = "Overview";
    private string _scope = nameof(WorkspaceScope.Global);
    private string _agentId = "codex";
    private string? _projectPath;
    private WorkspaceProjectChoice? _selectedProject;
    private GitImpactSnapshot? _gitImpactSnapshot;
    private int _gitImpactRequest;

    public WorkspaceViewModel(IWorkspaceService workspace, IGitImpactService gitImpact)
    {
        _workspace = workspace;
        _gitImpact = gitImpact;
        Scopes = Enum.GetNames<WorkspaceScope>();
        Agents = ["codex", "claude", "gemini"];
        _workspace.Changed += Reload;
        Reload();
    }

    public IReadOnlyList<string> Scopes { get; }
    public IReadOnlyList<string> Agents { get; }
    public ObservableCollection<ProjectGroup> Groups { get; } = [];
    public ObservableCollection<WorkspaceProjectChoice> ProjectChoices { get; } = [];
    public IReadOnlyList<string> Pages { get; } = [
        "Overview", "Projects", "Sessions", "Activity", "Git Impact", "Attention", "Goals",
        "Skills Manager", "AGENTS.md Studio", "Work Planner", "Agent Doctor", "MCP Manager",
        "Project Setup", "Agent Tools", "Settings"];

    public string Page { get => _page; set => Set(ref _page, value); }
    public string Scope { get => _scope; set => Set(ref _scope, value); }
    public string AgentId { get => _agentId; set => Set(ref _agentId, value); }
    public string? ProjectPath { get => _projectPath; set => Set(ref _projectPath, value); }
    public WorkspaceProjectChoice? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (!Set(ref _selectedProject, value)) return;
            ProjectPath = value?.Path;
        }
    }
    public WorkspaceContext Current => _workspace.Current;
    public IReadOnlyList<ProjectDescriptor> Projects => _workspace.Projects;
    public string ContextLabel => Current.Label;
    public string ProjectSummary => Projects.Count == 0 ? "No projects added" : $"{Projects.Count} worktree folders across {Groups.Count} repositories";
    public string SettingsPath => _workspace.SettingsPath;
    public GitImpactSnapshot? GitImpact { get => _gitImpactSnapshot; private set => Set(ref _gitImpactSnapshot, value); }

    public WorkspaceContext DraftContext()
    {
        if (!Enum.TryParse<WorkspaceScope>(Scope, ignoreCase: true, out var selectedScope))
            selectedScope = WorkspaceScope.Global;
        return new WorkspaceContext(selectedScope, AgentId, selectedScope == WorkspaceScope.Project ? ProjectPath : null);
    }

    public void Reload()
    {
        var current = _workspace.Current;
        Scope = current.Scope.ToString();
        AgentId = current.AgentId;
        ProjectPath = current.ProjectPath;
        Groups.Clear();
        foreach (var group in _workspace.Groups) Groups.Add(group);
        ProjectChoices.Clear();
        ProjectChoices.Add(new WorkspaceProjectChoice("Choose project…", null));
        foreach (var project in Projects)
            ProjectChoices.Add(new WorkspaceProjectChoice($"{project.Name} · {project.Path}", project.Path));
        SelectedProject = ProjectChoices.FirstOrDefault(item => item.Path == ProjectPath) ?? ProjectChoices[0];
        OnPropertyChanged(nameof(Current));
        OnPropertyChanged(nameof(ContextLabel));
        OnPropertyChanged(nameof(Projects));
        OnPropertyChanged(nameof(ProjectSummary));
        OnPropertyChanged(nameof(SettingsPath));
    }

    public string PreviewAddProject(string path) => _workspace.PreviewAddProject(path);
    public void AddProject(string path) => _workspace.AddProject(path);
    public string PreviewRemoveProject(string path) => _workspace.PreviewRemoveProject(path);
    public void RemoveProject(string path) => _workspace.RemoveProject(path);
    public string PreviewContext() => _workspace.PreviewContext(DraftContext());
    public void ApplyContext() => _workspace.SetContext(DraftContext());

    public async Task RefreshGitImpactAsync(CancellationToken cancellationToken = default)
    {
        var request = Interlocked.Increment(ref _gitImpactRequest);
        var projectPath = Current.Scope == WorkspaceScope.Project ? Current.ProjectPath : null;
        var result = await _gitImpact.ReadAsync(projectPath, cancellationToken);
        if (request == Volatile.Read(ref _gitImpactRequest)) GitImpact = result;
    }
}
