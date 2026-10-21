namespace CodexMonitor.Plugins.Codex;

internal sealed class CodexPlugin : IUsagePlugin
{
    public PluginInfo Info { get; } = new(
        "codex", "Codex", "Built-in plugin",
        Array.AsReadOnly<UsageWindowOption>([
            new("5-hour", "5-hour limit"), new("weekly", "Weekly limit")]),
        SupportsTokens: true, CanRepair: true);

    public Task<UsageSnapshot> ReadLimitsAsync(CancellationToken cancellationToken) =>
        CodexCliService.ReadAsync(cancellationToken);

    public Task<TokenUsageSnapshot> ReadTokensAsync(CancellationToken cancellationToken) =>
        CodexTokenClient.ReadAsync(cancellationToken);

    public Task RepairAsync(CancellationToken cancellationToken) =>
        CodexCliService.InstallOrRepairAsync(cancellationToken);
}
