namespace CodexMonitor.Core.Workspace;

public sealed record WorkspaceInstructionFile(string Path, long SizeBytes);

public interface IWorkspaceInstructionsInspector
{
    IReadOnlyList<WorkspaceInstructionFile> Find(string projectDirectory);
    string Read(string filePath);
}
