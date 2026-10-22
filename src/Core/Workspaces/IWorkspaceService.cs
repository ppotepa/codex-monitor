namespace CodexMonitor.Core.Workspaces;

public interface IWorkspaceService
{
    event Action? Changed;
    WorkspaceContext Current { get; }
    IReadOnlyList<ProjectDescriptor> Projects { get; }
    IReadOnlyList<ProjectGroup> Groups { get; }
    string SettingsPath { get; }
    string PreviewAddProject(string path);
    void AddProject(string path);
    string PreviewRemoveProject(string path);
    void RemoveProject(string path);
    string PreviewContext(WorkspaceContext context);
    void SetContext(WorkspaceContext context);
}
