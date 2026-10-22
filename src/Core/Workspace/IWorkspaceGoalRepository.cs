namespace CodexMonitor.Core.Workspace;

public sealed record WorkspaceGoal(string ProjectPath, string Title, int BudgetMinutes, DateTimeOffset StartedAtUtc);

public interface IWorkspaceGoalRepository
{
    WorkspaceGoal? Find(string projectPath);
    void Save(WorkspaceGoal goal);
}
