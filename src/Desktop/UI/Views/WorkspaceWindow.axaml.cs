using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodexMonitor.UI.ViewModels;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.UI.Views;

// Separate from the small tray popup: both windows share the same usage view model.
public partial class WorkspaceWindow : Window
{
    public WorkspaceWindow() : this(new DashboardViewModel()) { }

    internal WorkspaceWindow(DashboardViewModel usage, IWorkspaceGoalRepository? goals = null, IWorkspaceProjectRepository? projects = null, IWorkspaceInstructionsInspector? inspector = null,
        IGitImpactService? git = null, IWorkspaceActivitySource? activity = null, IWorkspacePlanner? planner = null, ILocalSkillsService? skills = null)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = new WorkspaceViewModel(usage, goals, projects, inspector, git, activity, planner, skills);
    }

    internal void ApplyUsage(CodexMonitor.Core.Usage.UsageSnapshot? snapshot)
    {
        if (DataContext is WorkspaceViewModel vm) vm.ApplyUsageSnapshot(snapshot);
    }
}
