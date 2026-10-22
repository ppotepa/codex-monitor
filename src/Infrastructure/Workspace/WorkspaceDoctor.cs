using CodexMonitor.Core.Agents;
using CodexMonitor.Core.Abstractions;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

public sealed class WorkspaceDoctor(
    IAgentManager agentManager,
    IExecutableLocator executables,
    IWorkspaceActivitySource activity,
    ILocalSkillsService skills,
    IWorkspaceInstructionsInspector instructions) : IWorkspaceDoctor
{
    public IReadOnlyList<WorkspaceDoctorCheck> Inspect(string? projectPath)
    {
        var checks = new List<WorkspaceDoctorCheck>();
        agentManager.Refresh();
        foreach (var agent in agentManager.Snapshot.Agents)
        {
            var state = !agent.IsInstalled ? "NotInstalled" :
                !agent.IsSupported ? "NotImplemented" : "Ready";
            var description = agent.IsInstalled
                ? agent.IsSupported
                    ? "Installed; CodexMonitor has a provider adapter."
                    : "CLI detected, but CodexMonitor does not implement this provider yet."
                : "CLI not detected. Installing an agent is optional.";
            checks.Add(new WorkspaceDoctorCheck("Agents", agent.Definition.DisplayName, state, description));
        }
        checks.Add(new WorkspaceDoctorCheck("Tools", "Git",
            executables.Find(["git", "git.exe"]) is null ? "NotInstalled" : "Ready",
            "Used only to inspect local repositories."));
        try
        {
            var observed = activity.ReadRecent(null, 1);
            checks.Add(new WorkspaceDoctorCheck("Activity", "Codex notify/hooks",
                !observed.IsConfigured ? "NotConfigured" : "Partial",
                observed.Coverage));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            checks.Add(new WorkspaceDoctorCheck("Activity", "Codex notify/hooks", "Error", ex.Message));
        }
        try
        {
            checks.Add(new WorkspaceDoctorCheck("Skills", "Installed local skills", "Ready",
                skills.List(projectPath).Count + " skills visible. No cloud sign-in required."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            checks.Add(new WorkspaceDoctorCheck("Skills", "Installed local skills", "Error", ex.Message));
        }
        if (!string.IsNullOrWhiteSpace(projectPath) && Directory.Exists(projectPath))
        {
            try
            {
                checks.Add(new WorkspaceDoctorCheck("Instructions", "AGENTS.md", "Ready",
                    instructions.Find(projectPath).Count + " instruction files found."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                checks.Add(new WorkspaceDoctorCheck("Instructions", "AGENTS.md", "Error", ex.Message));
            }
        }
        return checks;
    }
}
