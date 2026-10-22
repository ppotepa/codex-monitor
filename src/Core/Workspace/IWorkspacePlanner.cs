using CodexMonitor.Core.Usage;

namespace CodexMonitor.Core.Workspace;

public sealed record WorkspaceAdvice(string Summary, string Evidence, string Confidence);

public interface IWorkspacePlanner
{
    WorkspaceAdvice Evaluate(UsageSnapshot? limits, WorkspaceActivitySnapshot activity,
        WorkspaceGoal? goal, DateTimeOffset now, AgentSessionSnapshot? sessions = null);
}

// Pure, deterministic fallback. Never assumes that missing event logs imply an idle agent.
public sealed class WorkspacePlanner : IWorkspacePlanner
{
    public WorkspaceAdvice Evaluate(UsageSnapshot? limits, WorkspaceActivitySnapshot activity,
        WorkspaceGoal? goal, DateTimeOffset now, AgentSessionSnapshot? sessions = null)
    {
        var window = limits?.MainWindow;
        if (window is null)
            return new("No usage reading yet. Refresh limits to enable scheduling suggestions.",
                activity.Coverage, "No quota evidence");

        var remaining = Math.Clamp(100 - window.UsedPercent, 0, 100);
        var untilReset = window.ResetsAt is { } reset ? reset - now : (TimeSpan?)null;
        var activeGoal = goal is null ? "" : $" Current goal: {goal.Title} ({goal.BudgetMinutes} min budget).";
        var evidence = $"{remaining:0}% remaining in {window.Name}; " +
                       (untilReset is { } duration
                           ? $"reset in {Math.Max(0, duration.TotalHours):0.#} h."
                           : "reset time unknown.") + " " + activity.Coverage;
        var liveApproval = sessions?.Sessions.Any(item => item.Status == "waitingOnApproval") == true;
        var recentApproval = activity.Events.Any(item =>
            item.EventType.Contains("PermissionRequest", StringComparison.OrdinalIgnoreCase) &&
            now - item.At < TimeSpan.FromMinutes(10));
        if (liveApproval || recentApproval)
            return new("A recent event may need approval. Check its session before allocating more quota." +
                activeGoal, evidence, "Partial observation");

        var observedWorking = sessions?.Sessions.Count(item => item.Status == "active") ?? 0;
        if (untilReset is { } time && time > TimeSpan.Zero && time <= TimeSpan.FromHours(4) &&
            remaining >= 5)
            return new($"Consider using the remaining {remaining:0}% on useful work before reset; " +
                "unused quota usually does not carry forward. " + (observedWorking > 0 ?
                $"This App Server reports {observedWorking} active session(s)." :
                "Check active sessions directly; incomplete telemetry cannot prove inactivity.") + "" +
                activeGoal, evidence, "Quota-based recommendation");

        if (remaining < 10 && (untilReset is null || untilReset > TimeSpan.FromHours(8)))
            return new("Very little quota remains, and a near-term reset is not confirmed. " +
                "Prioritize small tasks or work that needs no agent calls." + activeGoal,
                evidence, "Quota-based recommendation");

        return new("Continue prioritized work. Current quota does not suggest waiting for a reset." +
            activeGoal, evidence, "Quota-based recommendation");
    }
}
