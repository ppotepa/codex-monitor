namespace CodexMonitor.Core.Workspace;

public sealed record WorkspaceDoctorCheck(string Category, string Name, string Status, string Detail);

public interface IWorkspaceDoctor
{
    IReadOnlyList<WorkspaceDoctorCheck> Inspect(string? projectPath);
}
