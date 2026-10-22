namespace CodexMonitor.Core.Workspace;

public sealed record WorkspaceAiPlanningContext(
    string ProviderId,
    double? RemainingPercent,
    DateTimeOffset? ResetAtUtc,
    int ObservedSessions,
    int ObservedActiveSessions,
    int ObservedWaitingApprovals,
    string SessionCoverage,
    string? GoalTitle,
    int? GoalBudgetMinutes);

public sealed record WorkspaceAiRecommendation(string Action, string Summary, string Rationale);

public interface IWorkspaceAiPlanner : IFeature
{
    Task<WorkspaceAiRecommendation> AnalyzeAsync(WorkspaceAiPlanningContext context,
        CancellationToken cancellationToken);
}
