using System.Security.Cryptography;
using System.Text;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Infrastructure.Workspace;

public sealed class InstructionsStudioService : IInstructionsAuditService, IInstructionsFileEditor
{
    public InstructionsAudit Analyze(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Split('\n').Select(line => line.Trim())
            .Where(line => line.Length >= 12 && !line.StartsWith('#') && !line.StartsWith("<!--"))
            .Select(line => line.TrimStart('-', '*', ' ').ToLowerInvariant())
            .ToArray();
        var duplicates = lines.GroupBy(value => value).Where(group => group.Count() > 1).ToArray();
        var warnings = new List<string>();
        if (duplicates.Length > 0)
            warnings.Add(duplicates.Length + " repeated instruction lines; consider consolidating.");
        if (text.Length > 12_000)
            warnings.Add("Large instructions file; move task-specific procedures to skills when appropriate.");
        if (!text.Contains('#'))
            warnings.Add("No headings found; add sections for faster navigation.");
        return new InstructionsAudit(Math.Max(1, (int)Math.Ceiling(text.Length / 4.0)), warnings);
    }

    public void Apply(string filePath, InstructionsProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var name = Path.GetFileName(filePath);
        if (name is not ("AGENTS.md" or "AGENTS.override.md"))
            throw new InvalidOperationException("Only AGENTS instruction files can be edited.");
        if ((File.GetAttributes(filePath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Cannot edit a symlinked instruction file.");
        var currentText = File.ReadAllText(filePath);
        var currentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(currentText)));
        if (!string.Equals(currentHash, proposal.OriginalSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The instructions changed since AI review. Analyze again.");
        if (string.IsNullOrWhiteSpace(proposal.ProposedText) || proposal.ProposedText.Length > 64_000)
            throw new InvalidDataException("AI proposed invalid or excessively long instructions.");
        var backup = filePath + ".bak." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
        File.Copy(filePath, backup, overwrite: false);
        var temp = filePath + ".codexmonitor.tmp";
        try
        {
            File.WriteAllText(temp, proposal.ProposedText);
            File.Move(temp, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
