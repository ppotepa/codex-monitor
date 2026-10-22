namespace CodexMonitor.Core.Workspace;

public interface IWorkspaceProjectRepository
{
    IReadOnlyList<string> List();
    void Add(string path);
    void Remove(string path);
}
