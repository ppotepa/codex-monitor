namespace CodexMonitor.Core.Agents;

public sealed record AgentDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ExecutableNames,
    string DataSource,
    string? ProviderId);

public sealed record DetectedAgent(AgentDefinition Definition, string? ExecutablePath)
{
    public bool IsInstalled => ExecutablePath is not null;
    public bool IsSupported => Definition.ProviderId is not null;
}

public sealed record AgentCatalogSnapshot(
    DateTimeOffset ScannedAt,
    IReadOnlyList<DetectedAgent> Agents);

public sealed record AgentSelection(
    IUsageProvider Provider,
    DetectedAgent? DetectedAgent,
    string Reason);

internal static class AgentCatalog
{
    // Detection is deliberately broader than support. Finding a CLI never means
    // that Codex Monitor will execute or read it without an adapter.
    // Unsupported agents are visible for detection only; adapters are DI registrations.
    public static IReadOnlyList<AgentDefinition> Known { get; } =
    [
        new("claude", "Claude Code", ["claude.exe", "claude.cmd"], "Claude Code CLI", null),
        new("gemini", "Gemini CLI", ["gemini.exe", "gemini.cmd"], "Gemini CLI", null),
        new("copilot", "GitHub Copilot CLI", ["copilot.exe", "copilot.cmd", "github-copilot-cli.exe"], "GitHub Copilot CLI", null),
        new("aider", "Aider", ["aider.exe", "aider.cmd"], "Aider CLI", null),
        new("opencode", "OpenCode", ["opencode.exe", "opencode.cmd"], "OpenCode CLI", null),
        new("goose", "Goose", ["goose.exe", "goose.cmd"], "Goose CLI", null)
    ];

    public static IReadOnlyList<AgentDefinition> Definitions(IReadOnlyList<IUsageProvider> providers)
    {
        var supported = providers.Select(provider => new AgentDefinition(provider.Info.Id,
            provider.Info.DisplayName, provider.Info.ExecutableNames, provider.Info.DataSource, provider.Info.Id));
        return supported.Concat(Known.Where(known => !providers.Any(provider =>
            provider.Info.Id.Equals(known.Id, StringComparison.OrdinalIgnoreCase)))).ToArray();
    }

    public static AgentSelection Select(
        AgentCatalogSnapshot snapshot,
        IReadOnlyList<IUsageProvider> providers,
        string? preferredAgentId)
    {
        var supported = snapshot.Agents.Where(agent => agent.IsSupported && agent.IsInstalled).ToArray();
        var preferred = supported.FirstOrDefault(agent =>
            string.Equals(agent.Definition.Id, preferredAgentId, StringComparison.OrdinalIgnoreCase));
        var candidate = preferred ?? supported.FirstOrDefault();
        if (candidate is not null)
        {
            var provider = providers.FirstOrDefault(item => item.Info.Id.Equals(
                candidate.Definition.ProviderId, StringComparison.OrdinalIgnoreCase));
            if (provider is not null)
                return new AgentSelection(provider, candidate,
                    preferred is null ? "First installed supported agent" : "Preferred agent");
        }

        // Keep the Codex adapter available so its repair flow can install the
        // missing dependency and recover on the next refresh.
        var fallback = providers.FirstOrDefault(item => item.Info.Id.Equals("codex", StringComparison.OrdinalIgnoreCase))
            ?? providers.First();
        var detectedUnsupported = snapshot.Agents.FirstOrDefault(agent => agent.IsInstalled && !agent.IsSupported);
        return new AgentSelection(fallback, null,
            detectedUnsupported is null
                ? "No installed supported agent; selected adapter can be repaired"
                : $"Detected {detectedUnsupported.Definition.DisplayName}, but no adapter is available");
    }
}

