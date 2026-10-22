using CodexMonitor.Infrastructure;
using CodexMonitor.Infrastructure.Persistence;
using CodexMonitor.Infrastructure.Processes;
using CodexMonitor.Providers.Codex;

const string actualShape = """
{
  "schema":"usage-v1","provider":"codex","plan":"prolite","status":"cached",
  "retrieved_at":"2026-10-21T07:05:56+00:00",
  "windows":[{"name":"weekly","seconds":604800,"used_pct":33,"resets_at":"2026-10-27T07:48:58+00:00"}]
}
""";
var weekly = CodexUsageParser.ParseLimits(actualShape);
Assert(weekly.MainWindow?.Name == "weekly", "Weekly-only account");
Assert(weekly.MainWindow?.UsedPercent == 33, "Used percentage");
Assert(weekly.MainWindow?.ResetsAt is not null, "Reset date");
Assert(weekly.Status == "cached", "Cache status");

const string multipleWindows = """
{"schema":"usage-v1","provider":"codex","windows":[
  {"name":"weekly","used_pct":80}, {"name":"5-hour","used_pct":25}]}
""";
var multiple = CodexUsageParser.ParseLimits(multipleWindows);
Assert(multiple.MainWindow?.Name == "5-hour", "Prefer 5-hour window");

try
{
    CodexUsageParser.ParseLimits("{\"schema\":\"unexpected\",\"provider\":\"codex\",\"windows\":[]}");
    throw new Exception("Unknown schema accepted");
}
catch (FormatException) { }

Assert(CodexCliService.IsBrokenLauncherError("did not find executable at 'C:\\Users\\name\\AppData\\Roaming\\uv\\python\\cpython...'"), "Missing Python launcher");
Assert(CodexCliService.IsBrokenLauncherError("Fatal error in launcher"), "Broken Python launcher");
Assert(!CodexCliService.IsBrokenLauncherError("authentication failed"), "Authentication error is not a launcher failure");
Assert(AlertPolicy.Level(79.9, 80, 95) == 0, "Below warning");
Assert(AlertPolicy.Level(80, 80, 95) == 1, "At warning");
Assert(AlertPolicy.Level(95, 80, 95) == 2, "At critical");
Assert(!AlertPolicy.ShouldNotify(2, new WindowAlertState(10, 1, true)), "Snoozed alert");
Assert(AlertPolicy.ShouldNotify(2, null), "New alert cycle has no previous state");

var now = DateTimeOffset.UtcNow;
var historyStore = new MonitorDatabase(Path.Combine(Path.GetTempPath(),
    $"codex-monitor-history-{Guid.NewGuid():N}.json"));
historyStore.ReplaceLimitHistory([new UsageHistoryPoint(now.AddDays(-8), "weekly", 10, 1, "codex")]);
var history = UsageHistory.Load(historyStore);
Assert(history.Add(weekly, now, "codex"), "Record current reading and prune old reading");
Assert(history.Recent("weekly", now, "codex").Count == 1, "Only recent reading remains");
Assert(!history.Add(weekly, now.AddMinutes(1), "codex"), "Avoid duplicate reading within four minutes");
Assert(history.Add(weekly, now.AddMinutes(5), "codex"), "Sample after five minutes");
Assert(history.Recent("weekly", now.AddMinutes(5), "codex").Count == 2, "Two chart samples");

var registry = new ProviderCatalog([new CodexProvider(new NullExecutableLocator(), new NullCommandLaunchStrategy(),
    new ProcessRunner(new NullCommandLaunchStrategy()))]);
Assert(registry.All.Count == 1 && registry.All[0].Info.Id == "codex", "Only Codex is registered");
Assert(registry.Get("CODEX").Info.SupportsTokens, "Codex advertises token support");
Assert(registry.Get("codex").Info.CanRepair, "Codex advertises dependency repair");
var detectedAgents = new AgentCatalogSnapshot(DateTimeOffset.UtcNow, [
    new DetectedAgent(AgentCatalog.Definitions(registry.All).Single(agent => agent.Id == "claude"), "C:\\tools\\claude.cmd"),
    new DetectedAgent(AgentCatalog.Definitions(registry.All).Single(agent => agent.Id == "codex"), "C:\\tools\\codex.cmd")
]);
var selectedAgent = AgentCatalog.Select(detectedAgents, registry.All, "auto");
Assert(selectedAgent.Provider.Info.Id == "codex" && selectedAgent.DetectedAgent?.Definition.Id == "codex",
    "Select the installed supported agent over an unsupported detected agent");
var preferredSelection = AgentCatalog.Select(detectedAgents, registry.All, "codex");
Assert(preferredSelection.Reason == "Preferred agent", "Honor preferred agent selection");
var unsupportedOnly = new AgentCatalogSnapshot(DateTimeOffset.UtcNow, [
    new DetectedAgent(AgentCatalog.Definitions(registry.All).Single(agent => agent.Id == "claude"), "C:\\tools\\claude.cmd")
]);
var fallbackSelection = AgentCatalog.Select(unsupportedOnly, registry.All, "auto");
Assert(fallbackSelection.Provider.Info.Id == "codex" && fallbackSelection.DetectedAgent is null,
    "Keep Codex repair adapter when only unsupported agents are installed");
var defaultSettings = new MonitorSettings();
Assert(defaultSettings.LimitsRefreshMinutes == 5 && defaultSettings.TokenRefreshMinutes == 30,
    "Use gentle default refresh cadence");
Assert(defaultSettings.AgentScanMinutes == 30 && defaultSettings.AutoRefreshTokens,
    "Enable periodic agent scan and token refresh by default");
VerifyWorkspace();
VerifyGitImpact();
try
{
    registry.Get("unsupported-provider");
    throw new Exception("Unsupported provider accepted");
}
catch (NotSupportedException) { }

var tokenSnapshot = CodexTokenParser.ParseTokens("""
{"summary":{"lifetimeTokens":12345,"peakDailyTokens":-1},"dailyUsageBuckets":[
 {"startDate":"2026-10-20","tokens":100},
 {"startDate":"2026-10-19","tokens":0},
 {"startDate":"2026-10-20","tokens":120},
 {"startDate":"2026-10-21","tokens":"invalid"},
 {"startDate":"bad-date","tokens":42}]}
""", now);
Assert(tokenSnapshot.LifetimeTokens == 12345 && tokenSnapshot.PeakDailyTokens is null, "Optional nonnegative token summary");
Assert(tokenSnapshot.DailyBuckets.Count == 2 && tokenSnapshot.DailyBuckets[0].Tokens == 0 && tokenSnapshot.DailyBuckets[1].Tokens == 120,
    "Token dates sorted and deduplicated; zero is data; malformed entries ignored");
Assert(CodexTokenParser.ParseTokens("{\"summary\":{}}", now).DailyBuckets.Count == 0, "Absent daily history remains empty");
var databasePath = Path.Combine(Path.GetTempPath(), $"codex-monitor-db-{Guid.NewGuid():N}.json");
var database = new MonitorDatabase(databasePath);
database.SaveTokenUsage("codex", tokenSnapshot, now);
var reloadedDatabase = MonitorDatabase.Open(databasePath);
var cached = reloadedDatabase.ReadTokenUsage("CODEX");
Assert(cached?.Snapshot.LifetimeTokens == 12345 && cached.CachedAt == now, "Persist and reload token cache");
File.Delete(databasePath);

Console.WriteLine("Parser, provider, workspace and Git impact checks passed.");

static void VerifyWorkspace()
{
    var root = Path.Combine(Path.GetTempPath(), $"codex-monitor-workspace-{Guid.NewGuid():N}");
    try
    {
        var repository = Path.Combine(root, "repo");
        var worktree = Path.Combine(root, "repo-feature");
        var commonGit = Path.Combine(repository, ".git");
        var worktreeGit = Path.Combine(commonGit, "worktrees", "feature");
        Directory.CreateDirectory(commonGit);
        Directory.CreateDirectory(worktree);
        Directory.CreateDirectory(worktreeGit);
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {worktreeGit}{Environment.NewLine}");
        File.WriteAllText(Path.Combine(worktreeGit, "commondir"), Path.GetRelativePath(worktreeGit, commonGit));

        var settings = new MonitorSettings();
        var repositoryStore = new MemorySettingsRepository();
        var workspace = new WorkspaceService(settings, repositoryStore, new TestAppPaths(root), new ProjectCatalog());
        var preview = workspace.PreviewAddProject(repository);
        Assert(preview.Contains(Path.Combine(root, "settings.json"), StringComparison.Ordinal), "Project preview names the settings target");
        Assert(settings.ProjectPaths.Count == 0, "Project preview makes no settings changes");
        workspace.AddProject(repository);
        var nested = Path.Combine(worktree, "src");
        Directory.CreateDirectory(nested);
        workspace.AddProject(nested);
        Assert(workspace.Groups.Count == 1 && workspace.Groups[0].Worktrees.Count == 2,
            "Group Git worktrees while preserving their separate paths");

        var context = new WorkspaceContext(WorkspaceScope.Project, "codex", worktree);
        Assert(workspace.PreviewContext(context).Contains("To: Project", StringComparison.Ordinal), "Preview a project context selection");
        workspace.SetContext(context);
        Assert(workspace.Current.Scope == WorkspaceScope.Project && workspace.Current.ProjectPath == worktree,
            "Persist project context");
        Assert(workspace.PreviewRemoveProject(worktree).Contains("active context will return to Global", StringComparison.Ordinal),
            "Explain the effect of removing the selected project");
        workspace.RemoveProject(worktree);
        Assert(workspace.Current.Scope == WorkspaceScope.Global && workspace.Projects.Count == 1,
            "Return to global scope after removing active worktree");
        Assert(repositoryStore.Saves == 4, "Persist each approved workspace operation");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void VerifyGitImpact()
{
    var root = Path.Combine(Path.GetTempPath(), $"codex-monitor-git-impact-{Guid.NewGuid():N}");
    try
    {
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var runner = new FixtureProcessRunner([
            new ProcessResult(0, "## feature/demo\n M src/changed.cs\n?? new.txt\n", ""),
            new ProcessResult(0, "2\t1\tsrc/changed.cs\n", "")
        ]);
        var service = new GitImpactService(new FixedExecutableLocator("git"), runner, new ProjectCatalog());
        var impact = service.ReadAsync(root).GetAwaiter().GetResult();
        Assert(impact.Status == AvailabilityStatus.Ready && impact.Branch == "feature/demo", "Read worktree status and branch");
        Assert(impact.ChangedFiles == 2 && impact.UntrackedFiles == 1, "Count tracked and untracked files");
        Assert(impact.AddedLines == 2 && impact.RemovedLines == 1, "Read added and removed lines");
        Assert(impact.Files.Any(file => file.Path == "new.txt" && file.ChangeType == "??"), "Keep untracked paths visible");

        var unavailableGit = new GitImpactService(new NullExecutableLocator(), runner, new ProjectCatalog());
        Assert(unavailableGit.ReadAsync(root).GetAwaiter().GetResult().Status == AvailabilityStatus.NotInstalled,
            "Expose missing Git as NotInstalled");
        Assert(service.ReadAsync(null).GetAwaiter().GetResult().Status == AvailabilityStatus.NoData,
            "Require a selected project for Git impact");
    }
    finally
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

internal sealed class NullExecutableLocator : IExecutableLocator
{
    public string? Find(IEnumerable<string> executableNames) => null;
}

internal sealed class FixedExecutableLocator(string executable) : IExecutableLocator
{
    public string? Find(IEnumerable<string> executableNames) => executable;
}

internal sealed class FixtureProcessRunner(IReadOnlyList<ProcessResult> results) : IProcessRunner
{
    private int _index;

    public Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (_index >= results.Count) throw new InvalidOperationException("No process fixture remains.");
        return Task.FromResult(results[_index++]);
    }
}

internal sealed class NullCommandLaunchStrategy : ICommandLaunchStrategy
{
    public System.Diagnostics.ProcessStartInfo Create(string executable, IReadOnlyList<string> arguments, bool redirectStandardInput)
        => throw new NotSupportedException();
}

internal sealed class TestAppPaths(string dataDirectory) : IAppPaths
{
    public string DataDirectory { get; } = dataDirectory;
}

internal sealed class MemorySettingsRepository : IMonitorSettingsRepository
{
    public int Saves { get; private set; }
    public string? PersistenceError => null;
    public MonitorSettings Load() => new();
    public void Save(MonitorSettings settings) => Saves++;
}
