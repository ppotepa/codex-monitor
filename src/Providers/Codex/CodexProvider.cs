using CodexMonitor.Providers.Codex.Limits;
using CodexMonitor.Providers.Codex.Tokens;

namespace CodexMonitor.Providers.Codex;

[UsageProvider("codex", "Codex",
    Description = "Built-in provider",
    DataSource = "Codex CLI app-server and codex-cli-usage",
    ExecutableNames = new[] { "codex.exe", "codex.cmd" })]
internal sealed class CodexProvider : IUsageProvider, ITokenUsageProvider, IRepairableUsageProvider
{
    private readonly CodexCliService _cli;
    private readonly CodexTokenClient _tokens;

    public CodexProvider(IExecutableLocator locator, ICommandLaunchStrategy launchStrategy, IProcessRunner processRunner)
    {
        _cli = new CodexCliService(locator, processRunner);
        _tokens = new CodexTokenClient(locator, launchStrategy);
    }

    public ProviderInfo Info { get; } = ProviderInfo.From<CodexProvider>(
        Array.AsReadOnly<UsageWindowOption>([
            new("5-hour", "5-hour limit"), new("weekly", "Weekly limit")]));

    public Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken) =>
        _cli.ReadAsync(cancellationToken);

    public Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken) =>
        _tokens.ReadAsync(cancellationToken);

    public Task RepairAsync(CancellationToken cancellationToken) =>
        _cli.InstallOrRepairAsync(cancellationToken);
}
