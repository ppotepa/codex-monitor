using System.Security.Cryptography;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

public sealed class LocalSkillsService : ILocalSkillsService
{
    private const int MaxFiles = 200;
    private const long MaxBytes = 8 * 1024 * 1024;

    public IReadOnlyList<LocalSkill> List(string? projectFolder)
    {
        var result = new List<LocalSkill>();
        Scan(GetSkillsRoot(SkillScope.User, null), SkillScope.User);
        if (!string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder))
            Scan(GetSkillsRoot(SkillScope.Project, projectFolder), SkillScope.Project);
        return result.OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        void Scan(string root, SkillScope scope)
        {
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var manifest = Path.Combine(dir, "SKILL.md");
                if (!File.Exists(manifest)) continue;
                var name = Path.GetFileName(dir);
                var description = "";
                foreach (var line in File.ReadLines(manifest).Take(40))
                {
                    if (!line.TrimStart().StartsWith("description:", StringComparison.OrdinalIgnoreCase)) continue;
                    description = line[(line.IndexOf(':') + 1)..].Trim().Trim('"', '\'');
                    break;
                }
                result.Add(new LocalSkill(name, dir, scope, description));
            }
        }
    }

    public SkillInstallReview PreviewInstall(string sourceFolder, SkillScope scope, string? projectFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFolder);
        var source = Path.GetFullPath(sourceFolder);
        if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "SKILL.md")))
            throw new InvalidDataException("Select a skill folder containing SKILL.md.");
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
        if (name is "." or ".." || name.Length is < 1 or > 100 ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Invalid skill folder name.");
        var root = GetSkillsRoot(scope, projectFolder);
        GuardPath(root);
        var target = Path.Combine(root, name);
        if (Directory.Exists(target) || File.Exists(target))
            throw new IOException("A skill with this name already exists at the chosen scope.");

        var reviewed = ReadFiles(source);
        var warnings = reviewed.Any(file => file.RelativePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase) ||
                                            file.RelativePath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) ||
                                            file.RelativePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            ? new[] { "This skill contains scripts. Review them; CodexMonitor will not execute them." }
            : Array.Empty<string>();
        return new SkillInstallReview(name, source, target, scope, reviewed, warnings);
    }

    public void Install(SkillInstallReview review)
    {
        ArgumentNullException.ThrowIfNull(review);
        var root = Path.GetDirectoryName(review.TargetFolder)!;
        GuardPath(root);
        var project = review.Scope == SkillScope.Project ? Path.GetDirectoryName(Path.GetDirectoryName(root)!) : null;
        var current = PreviewInstall(review.SourceFolder, review.Scope, project);
        if (!string.Equals(current.TargetFolder, review.TargetFolder, StringComparison.OrdinalIgnoreCase) ||
            current.Files.Count != review.Files.Count ||
            current.Files.Where((file, index) =>
                file.RelativePath != review.Files[index].RelativePath ||
                file.Sha256 != review.Files[index].Sha256).Any())
            throw new InvalidOperationException("Skill files have changed since the review. Review again.");
        Directory.CreateDirectory(root);
        var temp = Path.Combine(root, ".install-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            foreach (var file in review.Files)
            {
                var source = Path.Combine(review.SourceFolder, file.RelativePath);
                var dest = Path.Combine(temp, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(source, dest, overwrite: false);
                // A local source may change after preview. Verify the actual staged bytes
                // before the atomic directory move makes the skill visible.
                using var staged = File.OpenRead(dest);
                var copiedHash = Convert.ToHexString(SHA256.HashData(staged));
                if (!string.Equals(copiedHash, file.Sha256, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Skill source changed while installing. Nothing was installed; review again.");
            }
            Directory.Move(temp, review.TargetFolder);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }

    private static IReadOnlyList<SkillFileReview> ReadFiles(string source)
    {
        var files = new List<SkillFileReview>();
        var folders = new Queue<string>();
        folders.Enqueue(source);
        long total = 0;
        while (folders.Count > 0)
        {
            var directory = folders.Dequeue();
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symlinked skill folders are not supported.");
                if (Path.GetFileName(child) is ".git" or "node_modules") continue;
                folders.Enqueue(child);
            }
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Symlinked skill files are not supported.");
                var bytes = File.ReadAllBytes(path);
                total += bytes.Length;
                if (files.Count >= MaxFiles || total > MaxBytes)
                    throw new InvalidDataException("Skill exceeds the safe preview limits (200 files / 8 MB).");
                var relative = Path.GetRelativePath(source, path);
                files.Add(new SkillFileReview(relative, bytes.Length,
                    Convert.ToHexString(SHA256.HashData(bytes))));
            }
        }
        return files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static string GetSkillsRoot(SkillScope scope, string? projectFolder)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var baseFolder = scope == SkillScope.User ? home :
            !string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder)
                ? Path.GetFullPath(projectFolder)
                : throw new DirectoryNotFoundException("Select a project before installing a project skill.");
        return Path.Combine(baseFolder, ".agents", "skills");
    }

    private static void GuardPath(string root)
    {
        var parent = Path.GetDirectoryName(root);
        if (parent is null) throw new InvalidOperationException("Invalid skill target.");
        if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0 ||
            Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Skill target is a symlink.");
    }
}
