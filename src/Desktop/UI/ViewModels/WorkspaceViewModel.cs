using CodexMonitor.Core.Workspace;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CodexMonitor.UI.ViewModels;

// A presentation-only model. Local tools never require an account or network access.
public sealed record DoctorCheckItem(string Category, string Name, string Status, string Detail);
public sealed record SessionItem(string Name, string State, string Project, string LastUpdated, string Id);

public sealed class WorkspaceViewModel : BindableBase, IDisposable
{
    private readonly IWorkspaceGoalRepository? _goals;
    private readonly IWorkspaceProjectRepository? _projects;
    private readonly IWorkspaceInstructionsInspector? _inspector;
    private readonly IGitImpactService? _git;
    private readonly IWorkspaceActivitySource? _activity;
    private readonly IWorkspacePlanner? _planner;
    private readonly ILocalSkillsService? _skillsService;
    private readonly IInstructionsAuditService? _instructionAudit;
    private readonly IInstructionsOptimizer? _instructionOptimizer;
    private readonly IInstructionsFileEditor? _instructionEditor;
    private readonly IWorkspaceDoctor? _doctor;
    private readonly IWorkspaceAiPlanner? _aiPlanner;
    private bool _isAdvisorBusy;
    private DateTimeOffset? _lastAiAdvisorAt;
    private string _aiAdvisorText = "No AI analysis yet. Local recommendations are shown above.";
    private string _aiAdvisorStatus = "AI is optional and uses existing Codex CLI credentials.";
    private readonly IReadOnlyList<IAgentSessionSource> _sessionSources;
    private AgentSessionSnapshot? _sessionSnapshot;
    private string _selectedProviderId = "codex";
    private string _sessionsStatus = "Select Refresh to list available stored sessions.";
    private bool _isLoadingSessions;
    private string _doctorSummary = "Run local diagnostics to inspect agents and tools.";
    private readonly CancellationTokenSource _analysisLifetime = new();
    private InstructionsProposal? _pendingInstructions;
    private string? _proposalFile;
    private string _instructionsAuditText = "";
    private string _instructionsProposedText = "";
    private string _instructionsRationale = "AI suggestions appear here after an explicit request.";
    private string _instructionsOperationStatus = "";
    private bool _isOptimizingInstructions;
    private bool _confirmInstructionsApply;
    private SkillInstallReview? _skillPending;
    private readonly Dictionary<string, string> _knownSkillFolders = new(StringComparer.Ordinal); 
    private string _skillSourceFolder = "";
    private string _skillInstallScope = "User";
    private string _skillReviewText = "Choose a local skill directory and review it before installing.";
    private string _skillDetails = "Select an installed skill to read SKILL.md.";
    private string _skillInstallStatus = "";
    private string? _selectedSkill;
    private bool _confirmSkillInstall;
    private WorkspaceActivitySnapshot _activitySnapshot = new([], false, "Not configured", null);
    private WorkspaceGoal? _currentGoal;
    private CodexMonitor.Core.Usage.UsageSnapshot? _usageSnapshot;
    private string _gitBranch = "—";
    private string _gitImpactSelection = "Working tree";
    private string _gitDetails = "Select a Git project";
    private string _activityCoverage = "No observation source configured.";
    private string _activityAttention = "No confirmed events requiring attention.";
    private string _advisorEvidence = "";
    private string _notifySetup = "Add a Codex notify command in your existing Codex configuration.";
    private string? _selectedInstructionPath;
    private string? _selectedProject;
    private string _goalTitle = "";
    private string _budgetText = "60";
    private string _goalStatus = "Select a project to create a goal.";
    private string _projectPath = "";
    private string _projectStatus = "Choose a local Git repository to inspect.";
    private string _projectName = "No project selected";
    private string _gitSummary = "—";
    private string _instructions = "Select a project to read its AGENTS.md.";
    private string _skillsSummary = "Local skills are read-only in this version.";
    private string _advisorText = "Recommendations become available when limits have been read.";
    private bool _isLoading;
    private int _selectedPage;

    public DashboardViewModel Usage { get; }
    public string AiAdvisorText { get => _aiAdvisorText; private set => Set(ref _aiAdvisorText, value); }
    public string AiAdvisorStatus { get => _aiAdvisorStatus; private set => Set(ref _aiAdvisorStatus, value); }
    public bool IsAdvisorBusy { get => _isAdvisorBusy; private set => Set(ref _isAdvisorBusy, value); }
    public ObservableCollection<SessionItem> SessionItems { get; } = [];
    public string SessionsStatus { get => _sessionsStatus; private set => Set(ref _sessionsStatus, value); }
    public bool IsLoadingSessions { get => _isLoadingSessions; private set => Set(ref _isLoadingSessions, value); }
    public ObservableCollection<DoctorCheckItem> DoctorItems { get; } = [];
    public string DoctorSummary { get => _doctorSummary; private set => Set(ref _doctorSummary, value); }
    public ObservableCollection<string> ActivityItems { get; } = [];
    public string InstructionsAuditText { get => _instructionsAuditText; private set => Set(ref _instructionsAuditText, value); }
    public string InstructionsProposedText { get => _instructionsProposedText; private set => Set(ref _instructionsProposedText, value); }
    public string InstructionsRationale { get => _instructionsRationale; private set => Set(ref _instructionsRationale, value); }
    public string InstructionsOperationStatus { get => _instructionsOperationStatus; private set => Set(ref _instructionsOperationStatus, value); }
    public bool IsOptimizingInstructions { get => _isOptimizingInstructions; private set => Set(ref _isOptimizingInstructions, value); }
    public bool ConfirmInstructionsApply
    {
        get => _confirmInstructionsApply;
        set { if (Set(ref _confirmInstructionsApply, value)) OnPropertyChanged(nameof(CanApplyInstructions)); }
    }
    public bool CanApplyInstructions => _pendingInstructions is not null && ConfirmInstructionsApply;
    public IReadOnlyList<string> GitImpactScopes { get; } = ["Working tree", "Last commit"];
    public string GitImpactSelection { get => _gitImpactSelection; set => Set(ref _gitImpactSelection, value); }
    public string GitBranch { get => _gitBranch; private set => Set(ref _gitBranch, value); }
    public string GitDetails { get => _gitDetails; private set => Set(ref _gitDetails, value); }
    public string ActivityCoverage { get => _activityCoverage; private set => Set(ref _activityCoverage, value); }
    public string ActivityAttention { get => _activityAttention; private set => Set(ref _activityAttention, value); }
    public string AdvisorEvidence { get => _advisorEvidence; private set => Set(ref _advisorEvidence, value); }
    public string NotifySetup { get => _notifySetup; private set => Set(ref _notifySetup, value); }
    public ObservableCollection<string> InstructionFiles { get; } = [];
    public string? SelectedInstructionPath { get => _selectedInstructionPath; set => Set(ref _selectedInstructionPath, value); }
    public ObservableCollection<string> Projects { get; } = [];
    public string? SelectedProject { get => _selectedProject; set => Set(ref _selectedProject, value); }
    public ObservableCollection<string> ProjectFiles { get; } = [];
    public ObservableCollection<string> Skills { get; } = [];
    public IReadOnlyList<string> SkillScopes { get; } = ["User", "Project"];
    public string SkillSourceFolder { get => _skillSourceFolder; set => Set(ref _skillSourceFolder, value); }
    public string SkillInstallScope { get => _skillInstallScope; set => Set(ref _skillInstallScope, value); }
    public string SkillReviewText { get => _skillReviewText; private set => Set(ref _skillReviewText, value); }
    public string SkillDetails { get => _skillDetails; private set => Set(ref _skillDetails, value); }
    public string SkillInstallStatus { get => _skillInstallStatus; private set => Set(ref _skillInstallStatus, value); }
    public string? SelectedSkill { get => _selectedSkill; set => Set(ref _selectedSkill, value); }
    public bool ConfirmSkillInstall
    {
        get => _confirmSkillInstall;
        set { if (Set(ref _confirmSkillInstall, value)) OnPropertyChanged(nameof(CanInstallSkill)); }
    }
    public bool CanInstallSkill => _skillPending is not null && ConfirmSkillInstall;
    public string ProjectPath { get => _projectPath; set => Set(ref _projectPath, value); }
    public string GoalTitle { get => _goalTitle; set => Set(ref _goalTitle, value); }
    public string BudgetText { get => _budgetText; set => Set(ref _budgetText, value); }
    public string GoalStatus { get => _goalStatus; private set => Set(ref _goalStatus, value); }
    public string ProjectStatus { get => _projectStatus; private set => Set(ref _projectStatus, value); }
    public string ProjectName { get => _projectName; private set => Set(ref _projectName, value); }
    public string GitSummary { get => _gitSummary; private set => Set(ref _gitSummary, value); }
    public string Instructions { get => _instructions; private set => Set(ref _instructions, value); }
    public string SkillsSummary { get => _skillsSummary; private set => Set(ref _skillsSummary, value); }
    public string AdvisorText { get => _advisorText; private set => Set(ref _advisorText, value); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public int SelectedPage { get => _selectedPage; set => Set(ref _selectedPage, value); }
    public ICommand AskAiPlannerCommand { get; }
    public ICommand RefreshSessionsCommand { get; }
    public ICommand RefreshDoctorCommand { get; }
    public ICommand AnalyzeInstructionsCommand { get; }
    public ICommand OptimizeInstructionsCommand { get; }
    public ICommand ApplyInstructionsCommand { get; }
    public ICommand ReviewSkillCommand { get; }
    public ICommand InstallReviewedSkillCommand { get; }
    public ICommand ReadSelectedSkillCommand { get; }
    public ICommand RefreshActivityCommand { get; }
    public ICommand ReadInstructionCommand { get; }
    public ICommand LoadProjectCommand { get; }
    public ICommand SaveGoalCommand { get; }
    public ICommand InspectProjectCommand { get; }
    public ICommand RefreshSkillsCommand { get; }
    public ICommand ShowOverviewCommand { get; }
    public ICommand ShowProjectsCommand { get; }
    public ICommand ShowSkillsCommand { get; }
    public ICommand ShowInstructionsCommand { get; }

    public WorkspaceViewModel(DashboardViewModel usage, IWorkspaceGoalRepository? goals = null,
        IWorkspaceProjectRepository? projects = null, IWorkspaceInstructionsInspector? inspector = null,
        IGitImpactService? git = null, IWorkspaceActivitySource? activity = null,
        IWorkspacePlanner? planner = null, ILocalSkillsService? skillsService = null,
        IInstructionsAuditService? instructionAudit = null, IInstructionsOptimizer? instructionOptimizer = null,
        IInstructionsFileEditor? instructionEditor = null, IWorkspaceDoctor? doctor = null,
        IEnumerable<IAgentSessionSource>? sessionSources = null, IWorkspaceAiPlanner? aiPlanner = null)
    {
        Usage = usage;
        _goals = goals;
        _projects = projects;
        _inspector = inspector;
        _git = git;
        _activity = activity;
        _planner = planner;
        _skillsService = skillsService;
        _instructionAudit = instructionAudit;
        _instructionOptimizer = instructionOptimizer;
        _instructionEditor = instructionEditor;
        _doctor = doctor;
        _sessionSources = sessionSources?.ToArray() ?? [];
        _aiPlanner = aiPlanner;
        AskAiPlannerCommand = new UiCommand(() => _ = AskAiPlannerAsync());
        RefreshSessionsCommand = new UiCommand(() => _ = RefreshSessionsAsync());
        RefreshDoctorCommand = new UiCommand(RefreshDoctor);
        AnalyzeInstructionsCommand = new UiCommand(AnalyzeInstructions);
        OptimizeInstructionsCommand = new UiCommand(() => _ = OptimizeInstructionsAsync());
        ApplyInstructionsCommand = new UiCommand(ApplyInstructions);
        ReviewSkillCommand = new UiCommand(ReviewSkill);
        InstallReviewedSkillCommand = new UiCommand(InstallReviewedSkill);
        ReadSelectedSkillCommand = new UiCommand(ReadSelectedSkill);
        RefreshActivityCommand = new UiCommand(RefreshActivity);
        var exe = Environment.ProcessPath ?? "<CodexMonitor executable>";
        NotifySetup = "Add to ~/.codex/config.toml (with your executable path):\nnotify = [" +
            System.Text.Json.JsonSerializer.Serialize(exe) + ", \"--codex-notify\"]";
        ReadInstructionCommand = new UiCommand(ReadInstruction);
        LoadProjectCommand = new UiCommand(() => { if (!string.IsNullOrWhiteSpace(SelectedProject)) { ProjectPath = SelectedProject; _ = InspectProjectAsync(); } });
        LoadProjects();
        SaveGoalCommand = new UiCommand(SaveGoal);
        InspectProjectCommand = new UiCommand(() => _ = InspectProjectAsync());
        RefreshSkillsCommand = new UiCommand(RefreshSkills);
        ShowOverviewCommand = new UiCommand(() => SelectedPage = 0);
        ShowProjectsCommand = new UiCommand(() => SelectedPage = 1);
        ShowSkillsCommand = new UiCommand(() => SelectedPage = 4);
        ShowInstructionsCommand = new UiCommand(() => SelectedPage = 5);
        RefreshSkills();
        usage.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(DashboardViewModel.RemainingText) or nameof(DashboardViewModel.ResetText))
                RefreshAdvice();
        };
        RefreshAdvice();
    }

    private void RefreshAdvice()
    {
        if (_planner is not null)
        {
            var result = _planner.Evaluate(_usageSnapshot, _activitySnapshot,
                _currentGoal, DateTimeOffset.UtcNow, _sessionSnapshot);
            AdvisorText = result.Summary;
            AdvisorEvidence = result.Evidence;
            return;
        }
        AdvisorText = Usage.RemainingText == "No data"
            ? "Refresh limits to enable recommendations."
            : Usage.RemainingText + " · " + Usage.ResetText;
        AdvisorEvidence = "Quota only. No session data verified.";
    }

    // Called by the desktop host whenever a new provider snapshot is available.
    public void ApplyUsageSnapshot(CodexMonitor.Core.Usage.UsageSnapshot? limits, string providerId)
    {
        if (!string.Equals(_selectedProviderId, providerId, StringComparison.OrdinalIgnoreCase))
        {
            _sessionSnapshot = null;
            SessionItems.Clear();
            SessionsStatus = "Provider changed. Refresh sessions for the selected provider.";
        }
        _selectedProviderId = providerId;
        _usageSnapshot = limits;
        RefreshAdvice();
    }

    public void RefreshActivity()
    {
        ActivityItems.Clear();
        if (_activity is null)
        {
            ActivityCoverage = "Activity collection is not available.";
            return;
        }
        try
        {
            _activitySnapshot = _activity.ReadRecent(
                Directory.Exists(ProjectPath) ? Path.GetFullPath(ProjectPath) : null);
            ActivityCoverage = _activitySnapshot.Coverage;
            foreach (var ev in _activitySnapshot.Events)
                ActivityItems.Add($"{ev.At.ToLocalTime():g}  ·  {ev.EventType}  ·  {ev.SessionId ?? "unknown session"}");
            ActivityAttention = _activitySnapshot.Events.Any(ev =>
                ev.EventType.Contains("PermissionRequest", StringComparison.OrdinalIgnoreCase) &&
                DateTimeOffset.UtcNow - ev.At < TimeSpan.FromMinutes(10))
                ? "An observed event may need approval; verify its current state in Codex."
                : "No recent observed approval requests.";
            RefreshAdvice();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ActivityCoverage = "Could not read local activity: " + ex.Message;
        }
    }

    public async Task InspectProjectAsync()
    {
        if (IsLoading) return;
        var path = ProjectPath.Trim().Trim('"');
        if (!Directory.Exists(path))
        {
            ProjectStatus = "Directory not found. Enter an existing local folder.";
            return;
        }
        IsLoading = true;
        ProjectStatus = "Reading local repository…";
        try
        {
            ProjectName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
            ProjectFiles.Clear();
            if (_git is not null)
            {
                var scope = GitImpactSelection == "Last commit" ? GitImpactScope.LastCommit : GitImpactScope.WorkingTree;
                var impact = await _git.InspectRangeAsync(path, scope);
                GitBranch = impact.Branch + (impact.IsWorktree ? " · worktree" : "");
                GitSummary = $"+{impact.AddedLines:N0} / -{impact.DeletedLines:N0}";
                GitDetails = $"{impact.Files.Count} tracked changes · {impact.UntrackedFiles} untracked · {impact.LatestCommit}";
                ProjectStatus = (scope == GitImpactScope.LastCommit ? "Last committed revision" : "Working-tree changes") +
                    "; not attributed to any agent.";
                foreach (var file in impact.Files)
                    ProjectFiles.Add($"{file.Path}    +{file.Added} / -{file.Deleted}" +
                        (file.Binary ? " (binary)" : ""));
            }
            else
            {
                var result = await Task.Run(() => ReadGit(path));
                GitSummary = result.Summary;
                ProjectStatus = result.Status;
                foreach (var file in result.Files) ProjectFiles.Add(file);
            }
            RefreshInstructions(path);
            RefreshActivity();
            RefreshSkills();
            try
            {
                _projects?.Add(path);
                LoadProjects();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or InvalidOperationException)
            {
                ProjectStatus += " (Could not persist project: " + ex.Message + ")";
            }
            if (_goals is not null)
            {
                var saved = _goals.Find(path);
                GoalTitle = saved?.Title ?? "";
                BudgetText = saved?.BudgetMinutes.ToString() ?? "60";
                _currentGoal = saved;
                GoalStatus = saved is null ? "No goal saved for this repository." :
                    "Goal saved · " + saved.StartedAtUtc.ToLocalTime().ToString("g");
                RefreshAdvice();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
        {
            ProjectStatus = "Local inspection failed: " + ex.Message;
        }
        finally { IsLoading = false; }
    }

    private static (string Summary, string Status, string[] Files) ReadGit(string directory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("status");
        process.StartInfo.ArgumentList.Add("--short");
        process.StartInfo.ArgumentList.Add("--untracked-files=normal");
        if (!process.Start()) throw new InvalidOperationException("Unable to start Git.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(5000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Git status timed out.");
        }
        if (process.ExitCode != 0) return ("—", "Not a readable Git repository: " + stderr.Trim(), []);
        var files = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (files.Length + " changed/untracked files", "Working tree checked locally. Not attributed to a specific agent.", files.Take(100).ToArray());
    }

    private void RefreshInstructions(string project)
    {
        InstructionFiles.Clear();
        if (_inspector is null) return;
        try
        {
            foreach (var item in _inspector.Find(project)) InstructionFiles.Add(item.Path);
            if (InstructionFiles.Count > 0)
            {
                SelectedInstructionPath = InstructionFiles[0];
                ReadInstruction();
            }
            else
            {
                SelectedInstructionPath = null;
                Instructions = "No AGENTS.md or AGENTS.override.md found in the scanned folders.";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Instructions = "Could not inspect instructions: " + ex.Message;
        }
    }

    private void ReadInstruction()
    {
        _pendingInstructions = null;
        _proposalFile = null;
        ConfirmInstructionsApply = false;
        OnPropertyChanged(nameof(CanApplyInstructions));
        InstructionsProposedText = "";
        if (_inspector is null || string.IsNullOrWhiteSpace(SelectedInstructionPath)) return;
        try
        {
            Instructions = _inspector.Read(SelectedInstructionPath);
            AnalyzeInstructions();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        { Instructions = "Could not read instructions: " + ex.Message; }
    }

    public async Task AskAiPlannerAsync()
    {
        if (IsAdvisorBusy || _aiPlanner is null) return;
        if (!string.Equals(_aiPlanner.ProviderId, _selectedProviderId, StringComparison.OrdinalIgnoreCase))
        {
            AiAdvisorStatus = "AI planning is NotImplemented for this provider.";
            return;
        }
        if (_lastAiAdvisorAt is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(2))
        {
            AiAdvisorStatus = "Analysis cooldown: try again after two minutes to avoid unnecessary quota usage.";
            return;
        }
        IsAdvisorBusy = true;
        _lastAiAdvisorAt = DateTimeOffset.UtcNow;
        AiAdvisorStatus = "Requesting a structured JSON recommendation from the existing Codex CLI…";
        try
        {
            var primary = _usageSnapshot?.MainWindow;
            var context = new WorkspaceAiPlanningContext(_selectedProviderId,
                primary is null ? null : Math.Clamp(100 - primary.UsedPercent, 0, 100),
                primary?.ResetsAt, _sessionSnapshot?.Sessions.Count ?? 0,
                _sessionSnapshot?.Sessions.Count(item => item.Status == "active") ?? 0,
                _sessionSnapshot?.Sessions.Count(item => item.Status == "waitingOnApproval") ?? 0,
                _sessionSnapshot?.Coverage ?? "Unknown: stored sessions have not been inspected.",
                _currentGoal?.Title, _currentGoal?.BudgetMinutes);
            var answer = await _aiPlanner.AnalyzeAsync(context, _analysisLifetime.Token);
            AiAdvisorText = answer.Summary + Environment.NewLine + answer.Rationale;
            AiAdvisorStatus = "AI suggestion (" + answer.Action + "), not a verified system status.";
        }
        catch (OperationCanceledException)
        {
            AiAdvisorStatus = "AI analysis cancelled.";
        }
        catch (Exception ex)
        {
            AiAdvisorStatus = "Codex AI Advisor unavailable: " + ex.Message;
        }
        finally { IsAdvisorBusy = false; }
    }

    public async Task RefreshSessionsAsync()
    {
        if (IsLoadingSessions) return;
        IsLoadingSessions = true;
        SessionItems.Clear();
        var source = _sessionSources.FirstOrDefault(item =>
            item.ProviderId.Equals(_selectedProviderId, StringComparison.OrdinalIgnoreCase));
        if (source is null)
        {
            SessionsStatus = _selectedProviderId + " session integration: NotImplemented.";
            IsLoadingSessions = false;
            return;
        }
        SessionsStatus = "Reading stored sessions without resuming them…";
        try
        {
            _sessionSnapshot = await source.ListAsync(_analysisLifetime.Token);
            SessionsStatus = _sessionSnapshot.Sessions.Count + " stored sessions. " + _sessionSnapshot.Coverage;
            foreach (var session in _sessionSnapshot.Sessions)
            {
                var status = session.Status == "notLoaded" ? "Not loaded by this server" : session.Status;
                SessionItems.Add(new SessionItem(session.DisplayName, status,
                    session.ProjectDirectory ?? "Project unknown",
                    session.UpdatedAt?.ToLocalTime().ToString("g") ?? "No timestamp", session.Id));
            }
            RefreshAdvice();
        }
        catch (OperationCanceledException)
        {
            SessionsStatus = "Session read cancelled or timed out.";
        }
        catch (Exception ex)
        {
            SessionsStatus = "Could not query Codex App Server: " + ex.Message;
        }
        finally { IsLoadingSessions = false; }
    }

    public void RefreshDoctor()
    {
        DoctorItems.Clear();
        if (_doctor is null) { DoctorSummary = "Local diagnostics not available."; return; }
        try
        {
            foreach (var item in _doctor.Inspect(ProjectPath))
                DoctorItems.Add(new DoctorCheckItem(item.Category, item.Name, item.Status, item.Detail));
            DoctorSummary = "Local checks completed · " + DateTimeOffset.Now.ToString("g") +
                " · " + DoctorItems.Count + " checks. Unsupported agents remain visible.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            DoctorSummary = "Local diagnostics failed: " + ex.Message;
        }
    }

    public void AnalyzeInstructions()
    {
        if (_instructionAudit is null) return;
        try
        {
            var report = _instructionAudit.Analyze(Instructions);
            InstructionsAuditText = "Approx. " + report.ApproximateTokens +
                " tokens (character heuristic). " +
                (report.Warnings.Count == 0 ? "No obvious structural issues." :
                    string.Join(" ", report.Warnings));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            InstructionsAuditText = "Analysis failed: " + ex.Message;
        }
    }

    public async Task OptimizeInstructionsAsync()
    {
        if (IsOptimizingInstructions || _instructionOptimizer is null ||
            string.IsNullOrWhiteSpace(SelectedInstructionPath)) return;
        IsOptimizingInstructions = true;
        _pendingInstructions = null;
        _proposalFile = null;
        ConfirmInstructionsApply = false;
        OnPropertyChanged(nameof(CanApplyInstructions));
        InstructionsOperationStatus = "Codex is generating a read-only JSON proposal…";
        try
        {
            var selectedFile = SelectedInstructionPath;
            var original = Instructions;
            var proposal = await _instructionOptimizer.ProposeAsync(original, _analysisLifetime.Token);
            if (SelectedInstructionPath != selectedFile)
            {
                InstructionsOperationStatus = "Selection changed; proposal discarded.";
                return;
            }
            _pendingInstructions = proposal;
            _proposalFile = selectedFile;
            InstructionsProposedText = proposal.ProposedText;
            InstructionsRationale = proposal.Rationale;
            InstructionsOperationStatus = "Review the original and proposed text before confirming save.";
        }
        catch (OperationCanceledException)
        {
            InstructionsOperationStatus = "AI analysis cancelled.";
        }
        catch (Exception ex)
        {
            InstructionsOperationStatus = "AI proposal unavailable: " + ex.Message;
        }
        finally
        {
            IsOptimizingInstructions = false;
            OnPropertyChanged(nameof(CanApplyInstructions));
        }
    }

    public void ApplyInstructions()
    {
        if (!CanApplyInstructions || _instructionEditor is null ||
            _proposalFile != SelectedInstructionPath || _pendingInstructions is null) return;
        try
        {
            _instructionEditor.Apply(_proposalFile, _pendingInstructions);
            InstructionsOperationStatus = "Approved changes saved. A timestamped backup was retained.";
            ReadInstruction();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            InstructionsOperationStatus = "Could not save: " + ex.Message;
            _pendingInstructions = null;
            ConfirmInstructionsApply = false;
            OnPropertyChanged(nameof(CanApplyInstructions));
        }
    }

    public void Dispose() => _analysisLifetime.Cancel();

    private void LoadProjects()
    {
        Projects.Clear();
        if (_projects is null) return;
        try
        {
            foreach (var path in _projects.List()) Projects.Add(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ProjectStatus = "Could not read saved projects: " + ex.Message;
        }
    }

    public void SaveGoal()
    {
        if (_goals is null) { GoalStatus = "Goal storage is unavailable."; return; }
        if (!Directory.Exists(ProjectPath.Trim().Trim('\"')))
        { GoalStatus = "Select an existing project folder first."; return; }
        if (string.IsNullOrWhiteSpace(GoalTitle))
        { GoalStatus = "Enter a goal description."; return; }
        if (!int.TryParse(BudgetText, out var budget) || budget is < 1 or > 100000)
        { GoalStatus = "Budget must be an integer between 1 and 100000 minutes."; return; }
        try
        {
            _goals.Save(new WorkspaceGoal(Path.GetFullPath(ProjectPath.Trim().Trim('\"')),
                GoalTitle.Trim(), budget, DateTimeOffset.UtcNow));
            _currentGoal = _goals.Find(ProjectPath.Trim().Trim('"'));
            GoalStatus = "Goal saved locally. Time spent is not tracked automatically.";
            RefreshAdvice();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { GoalStatus = "Could not save goal: " + ex.Message; }
    }

    public void ReviewSkill()
    {
        _skillPending = null;
        ConfirmSkillInstall = false;
        OnPropertyChanged(nameof(CanInstallSkill));
        if (_skillsService is null) { SkillInstallStatus = "Skill management unavailable."; return; }
        try
        {
            var scope = SkillInstallScope == "Project" ? SkillScope.Project : SkillScope.User;
            _skillPending = _skillsService.PreviewInstall(SkillSourceFolder.Trim().Trim('"'),
                scope, ProjectPath.Trim().Trim('"'));
            var manifestPath = Path.Combine(_skillPending.SourceFolder, "SKILL.md");
            var manifestPreview = File.Exists(manifestPath) &&
                                  new FileInfo(manifestPath).Length <= 16_384
                ? File.ReadAllText(manifestPath)
                : "Manifest unavailable or larger than 16 KB; inspect the source manually.";
            SkillReviewText = "Install " + _skillPending.Name + " to " + _skillPending.TargetFolder +
                Environment.NewLine + "SKILL.md preview:" + Environment.NewLine + manifestPreview +
                Environment.NewLine + "Files (" + _skillPending.Files.Count + "):" +
                Environment.NewLine + string.Join(Environment.NewLine,
                    _skillPending.Files.Take(60).Select(f => f.RelativePath + " (" + f.SizeBytes + " bytes)")) +
                Environment.NewLine + string.Join(Environment.NewLine, _skillPending.Warnings);
            SkillInstallStatus = "Review the listed files, then explicitly approve installation.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SkillReviewText = "Skill preview failed: " + ex.Message;
            SkillInstallStatus = "";
        }
        OnPropertyChanged(nameof(CanInstallSkill));
    }

    public void InstallReviewedSkill()
    {
        if (!CanInstallSkill || _skillsService is null || _skillPending is null) return;
        try
        {
            _skillsService.Install(_skillPending);
            SkillInstallStatus = "Installed locally: " + _skillPending.Name;
            _skillPending = null;
            ConfirmSkillInstall = false;
            OnPropertyChanged(nameof(CanInstallSkill));
            RefreshSkills();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            SkillInstallStatus = "Installation failed: " + ex.Message + ". Review again before retry.";
            _skillPending = null;
            ConfirmSkillInstall = false;
            OnPropertyChanged(nameof(CanInstallSkill));
        }
    }

    public void ReadSelectedSkill()
    {
        if (SelectedSkill is null || !_knownSkillFolders.TryGetValue(SelectedSkill, out var folder)) return;
        try
        {
            var manifest = Path.Combine(folder, "SKILL.md");
            if (new FileInfo(manifest).Length > 256 * 1024)
                throw new InvalidDataException("SKILL.md exceeds 256 KB.");
            SkillDetails = File.ReadAllText(manifest);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SkillDetails = "Could not read skill manifest: " + ex.Message;
        }
    }

    public void RefreshSkills()
    {
        Skills.Clear();
        _knownSkillFolders.Clear();
        if (_skillsService is not null)
        {
            try
            {
                foreach (var skill in _skillsService.List(ProjectPath))
                {
                    var label = skill.Name + " · " + skill.Scope +
                        (string.IsNullOrWhiteSpace(skill.Description) ? "" : " · " + skill.Description);
                    Skills.Add(label);
                    _knownSkillFolders[label] = skill.Folder;
                }
                SkillsSummary = Skills.Count + " installed local skills.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SkillsSummary = "Could not list installed skills: " + ex.Message;
            }
            return;
        }
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agents", "skills"),
            Directory.Exists(ProjectPath.Trim().Trim('"')) ? Path.Combine(ProjectPath.Trim().Trim('"'), ".agents", "skills") : ""
        };
        SkillsSummary = "";
        foreach (var path in paths.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(path)) continue;
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(path))
                {
                    if (File.Exists(Path.Combine(directory, "SKILL.md")))
                        Skills.Add(Path.GetFileName(directory) + "  ·  " + path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SkillsSummary = "Some skill folders could not be read: " + ex.Message;
            }
        }
        if (!SkillsSummary.StartsWith("Some skill", StringComparison.Ordinal))
            SkillsSummary = Skills.Count + " local skills found. Installing or changing skills is not enabled here.";
    }
}
