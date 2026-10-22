using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.UI.ViewModels;

public sealed record OverviewProjectRow(
    string Path, string Name, string Subtitle, string SessionsText,
    string AddedText, string DeletedText, string State);

public sealed record OverviewActivityRow(string When, string Event, string Context);

// Overview is presentation of existing local data, not a second monitoring engine.
// Keep it in a partial ViewModel so the other tool views retain their behavior.
public sealed partial class WorkspaceViewModel
{
    private bool _overviewBusy;
    private bool _activityExpanded;
    private OverviewProjectRow? _selectedOverviewProject;
    private string _overviewStatus = "Reading local workspace…";
    private string _overviewTrackedCount = "0";
    private string _overviewActiveProjects = "No confirmed active projects";
    private string _overviewObservedSessions = "—";
    private string _overviewCoverage = "Session coverage unknown";
    private string _overviewResetClock = "—";
    private string _overviewResetDate = "Not reported";
    private double _overviewQuotaRemaining;
    private bool _overviewHasAttention;
    private string _overviewAttentionTitle = "Nothing confirmed";
    private string _overviewAttentionBody = "No recent requests requiring attention have been observed.";
    private string _overviewAttentionEvidence = "Missing telemetry is not proof that agents are idle.";

    public ObservableCollection<OverviewProjectRow> OverviewProjects { get; } = [];
    public ObservableCollection<OverviewActivityRow> OverviewActivity { get; } = [];

    public bool IsOverviewBusy { get => _overviewBusy; private set => Set(ref _overviewBusy, value); }
    public bool IsActivityExpanded { get => _activityExpanded; set => Set(ref _activityExpanded, value); }
    public bool HasOverviewProjects => OverviewProjects.Count > 0;
    public bool HasOverviewActivity => OverviewActivity.Count > 0;
    public string OverviewActivityCount => OverviewActivity.Count + " observed";
    public string OverviewStatus { get => _overviewStatus; private set => Set(ref _overviewStatus, value); }
    public string OverviewTrackedCount { get => _overviewTrackedCount; private set => Set(ref _overviewTrackedCount, value); }
    public string OverviewActiveProjects { get => _overviewActiveProjects; private set => Set(ref _overviewActiveProjects, value); }
    public string OverviewObservedSessions { get => _overviewObservedSessions; private set => Set(ref _overviewObservedSessions, value); }
    public string OverviewCoverage { get => _overviewCoverage; private set => Set(ref _overviewCoverage, value); }
    public string OverviewResetClock { get => _overviewResetClock; private set => Set(ref _overviewResetClock, value); }
    public string OverviewResetDate { get => _overviewResetDate; private set => Set(ref _overviewResetDate, value); }
    public double OverviewQuotaRemaining { get => _overviewQuotaRemaining; private set => Set(ref _overviewQuotaRemaining, value); }
    public bool HasOverviewAttention { get => _overviewHasAttention; private set => Set(ref _overviewHasAttention, value); }
    public bool NoOverviewAttention => !HasOverviewAttention;
    public string OverviewAttentionCount => HasOverviewAttention ? "1" : "0";
    public string OverviewAttentionTitle { get => _overviewAttentionTitle; private set => Set(ref _overviewAttentionTitle, value); }
    public string OverviewAttentionBody { get => _overviewAttentionBody; private set => Set(ref _overviewAttentionBody, value); }
    public string OverviewAttentionEvidence { get => _overviewAttentionEvidence; private set => Set(ref _overviewAttentionEvidence, value); }

    public OverviewProjectRow? SelectedOverviewProject
    {
        get => _selectedOverviewProject;
        set
        {
            if (!Set(ref _selectedOverviewProject, value) || value is null) return;
            ProjectPath = value.Path;
            SelectedPage = 1;
            _ = InspectProjectAsync();
            _selectedOverviewProject = null;
            OnPropertyChanged(nameof(SelectedOverviewProject));
        }
    }

    public ICommand RefreshOverviewCommand { get; private set; } = null!;
    public ICommand AddOverviewProjectCommand { get; private set; } = null!;
    public ICommand ShowOverviewActivityCommand { get; private set; } = null!;
    public ICommand ShowOverviewPlannerCommand { get; private set; } = null!;
    public ICommand ToggleOverviewActivityCommand { get; private set; } = null!;

    private void InitializeOverview()
    {
        RefreshOverviewCommand = new UiCommand(() =>
        {
            Usage.RefreshLimitsCommand.Execute(null);
            _ = RefreshOverviewAsync();
        });
        AddOverviewProjectCommand = new UiCommand(() => SelectedPage = 1);
        ShowOverviewActivityCommand = new UiCommand(() => SelectedPage = 7);
        ShowOverviewPlannerCommand = new UiCommand(() => SelectedPage = 6);
        ToggleOverviewActivityCommand = new UiCommand(() => IsActivityExpanded = !IsActivityExpanded);
        UpdateOverviewAllowance();
        if (_projects is not null)
        {
            try { OverviewTrackedCount = _projects.List().Count.ToString(CultureInfo.CurrentCulture); }
            catch (IOException) { OverviewStatus = "Saved projects could not be read."; }
            catch (UnauthorizedAccessException) { OverviewStatus = "Saved projects are not accessible."; }
        }
    }

    private void UpdateOverviewAllowance()
    {
        var window = _usageSnapshot?.MainWindow;
        OverviewQuotaRemaining = window is null ? 0 : Math.Clamp(100 - window.UsedPercent, 0, 100);
        var reset = window?.ResetsAt;
        if (reset is null)
        {
            OverviewResetClock = "—";
            OverviewResetDate = "Not reported";
            return;
        }

        var untilReset = reset.Value - DateTimeOffset.UtcNow;
        OverviewResetClock = untilReset <= TimeSpan.Zero ? "Due" :
            untilReset.Days > 0 ? $"{untilReset.Days}d {untilReset.Hours}h" :
            $"{(int)untilReset.TotalHours}h {untilReset.Minutes}m";
        OverviewResetDate = reset.Value.ToLocalTime().ToString("dd MMM · HH:mm", CultureInfo.CurrentCulture);
    }

    // Explicit refresh only; reading Overview never starts Codex app-server or AI.
    public async Task RefreshOverviewAsync()
    {
        if (IsOverviewBusy) return;
        IsOverviewBusy = true;
        OverviewStatus = "Updating local data…";
        try
        {
            _analysisLifetime.Token.ThrowIfCancellationRequested();
            var paths = _projects?.List() ?? [];
            OverviewTrackedCount = paths.Count.ToString(CultureInfo.CurrentCulture);

            var activity = _activity is null
                ? new WorkspaceActivitySnapshot([], false, "Not configured", null)
                : await Task.Run(() => _activity.ReadRecent(null, 80), _analysisLifetime.Token);
            _activitySnapshot = activity;
            var events = activity.Events
                .Where(item => string.Equals(item.ProviderId, _selectedProviderId,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.At)
                .ToArray();

            var sessions = _sessionSnapshot?.Sessions
                .Where(item => string.Equals(item.ProviderId, _selectedProviderId,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray() ?? [];
            var recentIds = events.Where(item => item.SessionId is not null)
                .Select(item => item.SessionId!).Distinct(StringComparer.Ordinal).Count();
            OverviewObservedSessions = _sessionSnapshot is not null
                ? sessions.Length.ToString(CultureInfo.CurrentCulture)
                : recentIds > 0 ? recentIds.ToString(CultureInfo.CurrentCulture) : "—";
            OverviewCoverage = _sessionSnapshot is not null
                ? "Stored sessions · partial coverage"
                : recentIds > 0 ? "Event identifiers · partial coverage"
                : activity.IsConfigured ? "No recent session IDs · partial" : "Not configured";

            var approval = events.FirstOrDefault(item =>
                DateTimeOffset.UtcNow - item.At is var age && age >= TimeSpan.Zero &&
                age < TimeSpan.FromMinutes(10) &&
                (item.EventType.Contains("PermissionRequest", StringComparison.OrdinalIgnoreCase) ||
                 item.EventType.Contains("waitingOnApproval", StringComparison.OrdinalIgnoreCase)));
            var reportedApproval = sessions.FirstOrDefault(item => item.Status == "waitingOnApproval");
            HasOverviewAttention = approval is not null || reportedApproval is not null;
            OnPropertyChanged(nameof(NoOverviewAttention));
            OnPropertyChanged(nameof(OverviewAttentionCount));
            if (reportedApproval is not null)
            {
                OverviewAttentionTitle = "Approval reported by Codex";
                OverviewAttentionBody = "A stored session reports waitingOnApproval. Verify the current state in Codex.";
                OverviewAttentionEvidence = "App Server response · coverage may be incomplete";
            }
            else if (approval is not null)
            {
                OverviewAttentionTitle = "Approval may be waiting";
                OverviewAttentionBody = "A Codex session reported a permission request. It might already be resolved.";
                OverviewAttentionEvidence = $"Observed {approval.At.ToLocalTime():g} via {approval.Origin}";
            }
            else
            {
                OverviewAttentionTitle = "No confirmed attention items";
                OverviewAttentionBody = "No recent approval requests observed.";
                OverviewAttentionEvidence = activity.Coverage;
            }

            OverviewActivity.Clear();
            foreach (var item in events.Take(8))
            {
                OverviewActivity.Add(new OverviewActivityRow(
                    item.At.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture),
                    item.EventType,
                    (item.ProjectDirectory is null ? "Project unknown" :
                        Path.GetFileName(Path.TrimEndingDirectorySeparator(item.ProjectDirectory))) +
                    " · " + item.Origin));
            }
            OnPropertyChanged(nameof(HasOverviewActivity));
            OnPropertyChanged(nameof(OverviewActivityCount));

            var pathsToShow = paths.Take(8).ToArray();
            using var concurrency = new SemaphoreSlim(3);
            var rows = await Task.WhenAll(pathsToShow.Select(async path =>
            {
                await concurrency.WaitAsync(_analysisLifetime.Token);
                try
                {
                    var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                    var projectSessions = sessions.Where(item => IsInsideProject(item.ProjectDirectory, path)).ToArray();
                    var latestEvent = events.FirstOrDefault(item => IsInsideProject(item.ProjectDirectory, path));
                    var status = projectSessions.Any(item => item.Status == "waitingOnApproval") ? "Approval?" :
                        projectSessions.Any(item => item.Status == "active") ? "Reported active" :
                        latestEvent is not null && DateTimeOffset.UtcNow - latestEvent.At < TimeSpan.FromMinutes(10)
                            ? "Recent event" : "No signal";
                    var sessionCount = projectSessions.Length > 0
                        ? projectSessions.Length + " stored"
                        : events.Where(item => item.SessionId is not null &&
                            IsInsideProject(item.ProjectDirectory, path))
                            .Select(item => item.SessionId!).Distinct().Any() ? "Observed events" : "—";
                    var branch = "Git unavailable";
                    var added = "—";
                    var removed = "";
                    if (_git is not null && Directory.Exists(path))
                    {
                        try
                        {
                            var git = await _git.InspectAsync(path, _analysisLifetime.Token);
                            branch = git.Branch;
                            added = "+" + git.AddedLines.ToString("N0", CultureInfo.CurrentCulture);
                            removed = "−" + git.DeletedLines.ToString("N0", CultureInfo.CurrentCulture);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
                        {
                            branch = "Git read unavailable";
                        }
                    }
                    return new OverviewProjectRow(path, name, path + " · " + branch,
                        sessionCount, added, removed, status);
                }
                finally { concurrency.Release(); }
            }));
            _analysisLifetime.Token.ThrowIfCancellationRequested();
            OverviewProjects.Clear();
            foreach (var row in rows) OverviewProjects.Add(row);
            OnPropertyChanged(nameof(HasOverviewProjects));

            var activeTracked = paths.Count(project => sessions.Any(item =>
                item.Status == "active" && IsInsideProject(item.ProjectDirectory, project)));
            OverviewActiveProjects = activeTracked > 0
                ? activeTracked + " reported active"
                : "No confirmed active projects";

            UpdateOverviewAllowance();
            RefreshAdvice();
            OverviewStatus = $"Local data checked {DateTimeOffset.Now:HH:mm} · " +
                (paths.Count > 8 ? "showing first 8 projects" : "partial session coverage");
        }
        catch (OperationCanceledException)
        {
            OverviewStatus = "Overview refresh cancelled.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            OverviewStatus = "Local overview unavailable: " + ex.Message;
        }
        finally { IsOverviewBusy = false; }
    }

    private static bool IsInsideProject(string? candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        try
        {
            var normalized = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                             Path.DirectorySeparatorChar;
            var directory = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return directory.StartsWith(normalized, comparison);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }
}
