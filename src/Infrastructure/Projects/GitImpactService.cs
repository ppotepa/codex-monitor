using System.Globalization;

namespace CodexMonitor.Infrastructure.Projects;

public sealed class GitImpactService(
    IExecutableLocator executables,
    IProcessRunner processes,
    ProjectCatalog projects) : IGitImpactService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(8);

    public async Task<GitImpactSnapshot> ReadAsync(string? projectPath, CancellationToken cancellationToken = default)
    {
        var observedAt = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(projectPath))
            return Empty(AvailabilityStatus.NoData, "Choose a project context to inspect its Git changes.", observedAt);

        var project = projects.TryResolve(projectPath);
        if (project is null)
            return Empty(AvailabilityStatus.Unavailable, "The selected project folder is no longer available.", observedAt, projectPath);
        if (!project.IsGitRepository)
            return Empty(AvailabilityStatus.NoData, "This project folder is not inside a Git worktree.", observedAt, project.Path);

        var git = executables.Find(["git.exe", "git"]);
        if (git is null)
            return Empty(AvailabilityStatus.NotInstalled, "Git CLI was not found on PATH.", observedAt, project.Path);

        try
        {
            var statusResult = await processes.RunAsync(git,
                ["-C", project.Path, "status", "--short", "--branch", "--untracked-files=normal"],
                CommandTimeout, cancellationToken);
            if (statusResult.ExitCode != 0)
                return Empty(AvailabilityStatus.Unavailable, CleanError(statusResult.StandardError), observedAt, project.Path);

            var statusLines = SplitLines(statusResult.StandardOutput);
            var branchLine = statusLines.FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal));
            var branch = branchLine is null ? null : branchLine[3..].Trim();
            var changes = statusLines.Where(line => !line.StartsWith("## ", StringComparison.Ordinal) && line.Length >= 3)
                .Select(ParseStatusLine).ToArray();

            var numstatResult = await processes.RunAsync(git,
                ["-C", project.Path, "diff", "--numstat", "HEAD", "--"],
                CommandTimeout, cancellationToken);
            var files = numstatResult.ExitCode == 0
                ? ParseNumstat(numstatResult.StandardOutput)
                : Array.Empty<GitImpactFile>();
            var added = files.Sum(file => file.AddedLines);
            var removed = files.Sum(file => file.RemovedLines);
            var trackedChanges = changes.Count(change => change.Type != "??");
            var untracked = changes.Length - trackedChanges;
            var summary = changes.Length == 0
                ? "Working tree is clean."
                : $"{changes.Length} changed file{(changes.Length == 1 ? "" : "s")} · {added} additions · {removed} deletions";

            var visibleFiles = changes.Select(change =>
            {
                var counts = files.FirstOrDefault(file => string.Equals(file.Path, change.Path, StringComparison.Ordinal));
                return new GitImpactFile(change.Path, counts?.AddedLines ?? 0, counts?.RemovedLines ?? 0, change.Type);
            }).Take(100).ToArray();

            return new GitImpactSnapshot(AvailabilityStatus.Ready, project.Path, summary, branch,
                changes.Length, untracked, added, removed, visibleFiles, observedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is ProcessLaunchException or TimeoutException or UnauthorizedAccessException or IOException)
        {
            return Empty(AvailabilityStatus.Unavailable, ex.Message, observedAt, project.Path);
        }
    }

    private static GitImpactSnapshot Empty(AvailabilityStatus status, string message, DateTimeOffset observedAt, string projectPath = "") =>
        new(status, projectPath, message, null, 0, 0, 0, 0, Array.Empty<GitImpactFile>(), observedAt,
            status == AvailabilityStatus.Ready ? null : message);

    private static string[] SplitLines(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static (string Path, string Type) ParseStatusLine(string line)
    {
        var type = line[..2].Trim();
        var path = line.Length > 3 ? line[3..] : line;
        return (path, string.IsNullOrWhiteSpace(type) ? "M" : type);
    }

    private static GitImpactFile[] ParseNumstat(string output) => SplitLines(output)
        .Select(line => line.Split('\t', 3))
        .Where(parts => parts.Length == 3)
        .Select(parts => new GitImpactFile(parts[2], ParseCount(parts[0]), ParseCount(parts[1]), "M"))
        .ToArray();

    private static int ParseCount(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
        ? count
        : 0;

    private static string CleanError(string error) => string.IsNullOrWhiteSpace(error)
        ? "Git could not read this worktree. Check the folder and repository permissions."
        : error.Trim();
}
