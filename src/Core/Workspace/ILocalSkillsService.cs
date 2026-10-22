namespace CodexMonitor.Core.Workspace;

public enum SkillScope { User, Project }
public sealed record LocalSkill(string Name, string Folder, SkillScope Scope, string Description);
public sealed record SkillFileReview(string RelativePath, long SizeBytes, string Sha256);
public sealed record SkillInstallReview(string Name, string SourceFolder, string TargetFolder,
    SkillScope Scope, IReadOnlyList<SkillFileReview> Files, IReadOnlyList<string> Warnings);

public interface ILocalSkillsService
{
    IReadOnlyList<LocalSkill> List(string? projectFolder);
    SkillInstallReview PreviewInstall(string sourceFolder, SkillScope scope, string? projectFolder);
    void Install(SkillInstallReview review);
}
