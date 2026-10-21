namespace CodexMonitor.Plugins.Codex;

[UsagePlugin("codex", "Codex",
    Description = "Built-in plugin",
    DataSource = "Codex CLI app-server and codex-cli-usage",
    ExecutableNames = new[] { "codex.exe", "codex.cmd" })]
internal sealed class CodexPlugin : IUsagePlugin, ITokenUsagePlugin, IRepairableUsagePlugin
{
    private readonly CodexCliService _cli;
    private readonly CodexTokenClient _tokens;

    public CodexPlugin(IExecutableLocator locator, ICommandLaunchStrategy launchStrategy)
    {
        _cli = new CodexCliService(locator);
        _tokens = new CodexTokenClient(locator, launchStrategy);
    }

    public PluginInfo Info { get; } = PluginInfo.From<CodexPlugin>(
        Array.AsReadOnly<UsageWindowOption>([
            new("5-hour", "5-hour limit"), new("weekly", "Weekly limit")]));

    public Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken) =>
        _cli.ReadAsync(cancellationToken);

    public Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken) =>
        _tokens.ReadAsync(cancellationToken);

    public Task RepairAsync(CancellationToken cancellationToken) =>
        _cli.InstallOrRepairAsync(cancellationToken);
}
