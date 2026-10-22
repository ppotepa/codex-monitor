using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CodexMonitor.Core.Workspace;
using CodexMonitor.Infrastructure.Workspace;
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

var goalsPath = Path.Combine(Path.GetTempPath(), $"codex-monitor-goals-{Guid.NewGuid():N}.json");
var goalStore = new JsonWorkspaceGoalRepository(goalsPath);
var goal = new WorkspaceGoal(Path.GetTempPath(), "Finish refactor", 90, now);
goalStore.Save(goal);
Assert(goalStore.Find(Path.GetTempPath())?.BudgetMinutes == 90, "Read stored goal");
goalStore.Save(goal with { Title = "Finish review", BudgetMinutes = 30 });
Assert(goalStore.Find(Path.GetTempPath())?.Title == "Finish review", "Replace goal for the same project");
try
{
    goalStore.Save(goal with { BudgetMinutes = 0 });
    throw new Exception("Invalid goal budget accepted");
}
catch (ArgumentOutOfRangeException) { }
File.WriteAllText(goalsPath, "{bad json");
try
{
    goalStore.Save(goal);
    throw new Exception("Corrupt goals file was overwritten");
}
catch (InvalidDataException) { }
File.Delete(goalsPath);

var projectsPath = Path.Combine(Path.GetTempPath(), $"codex-monitor-projects-{Guid.NewGuid():N}.json");
var projectStore = new JsonWorkspaceProjectRepository(projectsPath);
projectStore.Add(Path.GetTempPath());
projectStore.Add(Path.GetTempPath());
Assert(projectStore.List().Count == 1, "Project paths must be deduplicated");
projectStore.Remove(Path.GetTempPath());
Assert(projectStore.List().Count == 0, "Project removal");
File.Delete(projectsPath);

var instructionsFolder = Path.Combine(Path.GetTempPath(), $"codex-instructions-{Guid.NewGuid():N}");
Directory.CreateDirectory(Path.Combine(instructionsFolder, "src", "nested"));
Directory.CreateDirectory(Path.Combine(instructionsFolder, "node_modules"));
File.WriteAllText(Path.Combine(instructionsFolder, "AGENTS.md"), "Root rules");
File.WriteAllText(Path.Combine(instructionsFolder, "src", "nested", "AGENTS.override.md"), "Nested rules");
File.WriteAllText(Path.Combine(instructionsFolder, "node_modules", "AGENTS.md"), "Ignored rules");
var instructionsInspector = new FileSystemInstructionsInspector();
var discovered = instructionsInspector.Find(instructionsFolder);
Assert(discovered.Count == 2, "Root and nested AGENTS instructions discovered without dependency folders");
Assert(instructionsInspector.Read(discovered[0].Path).Length > 0, "Read safe instruction file");
Directory.Delete(instructionsFolder, recursive: true);


var skillsFixture = Path.Combine(Path.GetTempPath(), $"codex-skills-{Guid.NewGuid():N}");
var skillSource = Path.Combine(skillsFixture, "fixtures", "example-skill");
var skillProject = Path.Combine(skillsFixture, "repository");
Directory.CreateDirectory(skillSource);
Directory.CreateDirectory(skillProject);
File.WriteAllText(Path.Combine(skillSource, "SKILL.md"), "---\nname: example-skill\ndescription: Example local skill\n---");
var skillsService = new LocalSkillsService();
var reviewedSkill = skillsService.PreviewInstall(skillSource, SkillScope.Project, skillProject);
Assert(reviewedSkill.Files.Count == 1, "Skill preview contains manifest");
skillsService.Install(reviewedSkill);
Assert(skillsService.List(skillProject).Any(item => item.Name == "example-skill"), "Reviewed skill is installed locally");
try
{
    skillsService.PreviewInstall(skillSource, SkillScope.Project, skillProject);
    throw new Exception("Duplicate skill destination accepted");
}
catch (IOException) { }
Directory.Delete(skillsFixture, recursive: true);

var featureRegistry = new FeatureRegistry([
    new SampleFeature("codex", "sessions.list", 10),
    new SampleFeature("codex", "sessions.list", 5)
]);
Assert(featureRegistry.Candidates<SampleFeature>("CODEX").Count == 2, "Candidate fallback sorted by priority");
Assert(featureRegistry.Describe("codex", "sessions.list").Implemented, "Codex feature implemented");
Assert(!featureRegistry.Describe("claude", "sessions.list").Implemented, "Unsupported feature explicitly NotImplemented");

var observation = new WorkspaceActivitySnapshot([], false, "No hook configured", null);
var planner = new WorkspacePlanner();
var lowQuota = new UsageSnapshot("pro", now, [new UsageWindow("weekly", 87, now.AddHours(3))]);
Assert(planner.Evaluate(lowQuota, observation, null, now).Summary.Contains("remaining 13%"),
    "Planner recommends useful work instead of wasting quota near reset");
var tooLow = new UsageSnapshot("pro", now, [new UsageWindow("weekly", 97, now.AddDays(3))]);
Assert(planner.Evaluate(tooLow, observation, null, now).Summary.Contains("Very little quota"),
    "Planner warns about small quota and distant reset");
Assert(planner.Evaluate(null, observation, null, now).Confidence == "No quota evidence",
    "No invented recommendations without readings");

var skillWork = Path.Combine(Path.GetTempPath(), "codex-skill-tests-" + Guid.NewGuid().ToString("N"));
var installSkillSource = Path.Combine(skillWork, "sample-skill");
var skillTarget = Path.Combine(skillWork, "repository");
Directory.CreateDirectory(installSkillSource);
Directory.CreateDirectory(skillTarget);
File.WriteAllText(Path.Combine(installSkillSource, "SKILL.md"), "---\nname: sample-skill\ndescription: Tests only\n---\n");
var installSkillsService = new LocalSkillsService();
var skillReview = installSkillsService.PreviewInstall(installSkillSource, SkillScope.Project, skillTarget);
Assert(skillReview.Files.Count == 1 && skillReview.Scope == SkillScope.Project, "Review local skill contents");
installSkillsService.Install(skillReview);
Assert(installSkillsService.List(skillTarget).Any(skill => skill.Name == "sample-skill"), "Skill install and discovery");
try
{
    installSkillsService.PreviewInstall(installSkillSource, SkillScope.Project, skillTarget);
    throw new Exception("Duplicate skill was installable");
}
catch (IOException) { }
Directory.Delete(skillWork, recursive: true);

var instructionWork = Path.Combine(Path.GetTempPath(), "codex-instruction-edit-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(instructionWork);
var instructionPath = Path.Combine(instructionWork, "AGENTS.md");
var originalInstructions = "Always run the tests before merging.\nAlways run the tests before merging.\n";
File.WriteAllText(instructionPath, originalInstructions);
var instructionsStudio = new InstructionsStudioService();
Assert(instructionsStudio.Analyze(originalInstructions).Warnings.Count > 0, "Duplicate instruction detection");
var instructionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(originalInstructions)));
var proposal = new InstructionsProposal("codex", instructionHash, "Run tests before merging.\n", "Remove duplicate");
instructionsStudio.Apply(instructionPath, proposal);
Assert(File.ReadAllText(instructionPath) == proposal.ProposedText, "Approved instructions applied");
Assert(Directory.GetFiles(instructionWork, "AGENTS.md.bak.*").Length == 1, "Backup before rewrite");
try
{
    instructionsStudio.Apply(instructionPath, proposal);
    throw new Exception("Stale instructions proposal overwrote newer file");
}
catch (InvalidOperationException) { }
Directory.Delete(instructionWork, recursive: true);

var eventDir = Path.Combine(Path.GetTempPath(), "codex-event-tests-" + Guid.NewGuid().ToString("N"));
var eventPayload = JsonSerializer.Serialize(new Dictionary<string, string>
{
    ["type"] = "agent-turn-complete",
    ["thread-id"] = "thread-test",
    ["cwd"] = Path.GetTempPath(),
    ["last-assistant-message"] = "secret that must not be logged"
});
Assert(WorkspaceNotifyReceiver.Capture(["--codex-notify", eventPayload], eventDir) == 0,
    "Codex event receiver stores a notification");
var eventPath = Path.Combine(eventDir, "activity.jsonl");
Assert(!File.ReadAllText(eventPath).Contains("secret that must not be logged"),
    "Event journal does not persist assistant message text");
Assert(new JsonlWorkspaceActivitySource(eventPath).ReadRecent(null).Events.Count == 1,
    "Activity feed can load metadata-only events");
Directory.Delete(eventDir, recursive: true);


var gitFixture = Path.Combine(Path.GetTempPath(), $"codex-git-impact-{Guid.NewGuid():N}");
Directory.CreateDirectory(gitFixture);
try
{
    static void GitFixture(string folder, params string[] args)
    {
        using var p = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = folder,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start();
        var output = p.StandardOutput.ReadToEnd();
        var error = p.StandardError.ReadToEnd();
        if (!p.WaitForExit(5000)) { p.Kill(entireProcessTree: true); throw new Exception("Git fixture timed out"); }
        if (p.ExitCode != 0) throw new Exception("Git fixture failed: " + error);
    }

    GitFixture(gitFixture, "init", "-q");
    File.WriteAllText(Path.Combine(gitFixture, "code.txt"), "first\n");
    GitFixture(gitFixture, "add", "code.txt");
    GitFixture(gitFixture, "-c", "user.name=CodexMonitor Tests",
        "-c", "user.email=tests@example.invalid", "commit", "-qm", "first");
    File.AppendAllText(Path.Combine(gitFixture, "code.txt"), "second\n");
    var gitService = new GitImpactService();
    var working = await gitService.InspectRangeAsync(gitFixture, GitImpactScope.WorkingTree);
    Assert(working.AddedLines == 1 && working.DeletedLines == 0, "Working-tree impact");
    var committed = await gitService.InspectRangeAsync(gitFixture, GitImpactScope.LastCommit);
    Assert(committed.AddedLines == 1 && committed.DeletedLines == 0, "Last-commit impact");
}
finally
{
    DeleteTree(gitFixture);
}

Console.WriteLine("Parser, provider, capabilities, planner, skills, instructions and activity checks passed.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void DeleteTree(string path)
{
    if (!Directory.Exists(path)) return;
    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        File.SetAttributes(file, FileAttributes.Normal);
    foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                 .OrderByDescending(directory => directory.Length))
        File.SetAttributes(directory, FileAttributes.Normal);
    File.SetAttributes(path, FileAttributes.Normal);
    Directory.Delete(path, recursive: true);
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

internal sealed record SampleFeature(string ProviderId, string FeatureId, int Priority) : IFeature;
