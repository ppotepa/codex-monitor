namespace CodexMonitor.Core.Workspace;

public sealed record InstructionsAudit(int ApproximateTokens, IReadOnlyList<string> Warnings);
public sealed record InstructionsProposal(string ProviderId, string OriginalSha256, string ProposedText, string Rationale);

public interface IInstructionsAuditService
{
    InstructionsAudit Analyze(string text);
}

public interface IInstructionsOptimizer : IFeature
{
    Task<InstructionsProposal> ProposeAsync(string originalText, CancellationToken cancellationToken);
}

public interface IInstructionsFileEditor
{
    // Saves after a visible user confirmation. Rejects changed files and retains a backup.
    void Apply(string filePath, InstructionsProposal proposal);
}
