using System.Diagnostics;
using System.Globalization;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

// All commands are read-only. ArgumentList avoids invoking a shell.
public sealed class GitImpactService : IGitImpactService
{
    public async Task<GitImpactSnapshot> InspectAsync(string directory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        var root = (await RunAsync(directory, cancellationToken, "rev-parse", "--show-toplevel")).Trim();
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidDataException("Git did not return a repository root.");
        var branch = (await RunAsync(directory, cancellationToken, "rev-parse", "--abbrev-ref", "HEAD")).Trim();
        var gitDir = (await RunAsync(directory, cancellationToken, "rev-parse", "--git-dir")).Trim();
        var worktree = File.Exists(Path.Combine(root, ".git")) ||
                       gitDir.Contains("worktrees", StringComparison.OrdinalIgnoreCase);
        var latest = (await RunAsync(directory, cancellationToken, "log", "-1", "--format=%h %s")).Trim();
        var numstat = await RunAsync(directory, cancellationToken, "diff", "--numstat", "HEAD", "--");
        var status = await RunAsync(directory, cancellationToken, "ls-files", "--others", "--exclude-standard");
        var files = new List<GitFileImpact>();
        foreach (var line in numstat.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\t', 3);
            if (fields.Length != 3) continue;
            var binary = fields[0] == "-" || fields[1] == "-";
            var added = int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a) ? Math.Max(0, a) : 0;
            var removed = int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d) ? Math.Max(0, d) : 0;
            files.Add(new GitFileImpact(fields[2], added, removed, binary));
        }
        return new GitImpactSnapshot(root, branch,
            files.Sum(file => file.Added),
            files.Sum(file => file.Deleted),
            status.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length,
            files.Take(400).ToArray(), latest, worktree);
    }

    private static async Task<string> RunAsync(string directory, CancellationToken cancellationToken, params string[] arguments)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
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
        foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
        try
        {
            if (!process.Start()) throw new InvalidOperationException("Could not launch git.");
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var output = await stdout.ConfigureAwait(false);
            var error = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidOperationException("Git " + arguments[0] + " failed: " + error.Trim());
            if (output.Length > 2_000_000) throw new InvalidDataException("Git output exceeds 2 MB.");
            return output;
        }
        catch
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            }
            throw;
        }
    }
}
