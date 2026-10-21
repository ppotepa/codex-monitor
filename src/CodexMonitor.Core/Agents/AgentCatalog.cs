namespace CodexMonitor;

internal sealed record AgentDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> ExecutableNames,
    string DataSource,
    string? PluginId);

internal sealed record DetectedAgent(AgentDefinition Definition, string? ExecutablePath)
{
    public bool IsInstalled => ExecutablePath is not null;
    public bool IsSupported => Definition.PluginId is not null;
}

internal sealed record AgentCatalogSnapshot(
    DateTimeOffset ScannedAt,
    IReadOnlyList<DetectedAgent> Agents);

internal sealed record AgentSelection(
    IUsagePlugin Plugin,
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

    public static IReadOnlyList<AgentDefinition> Definitions(IReadOnlyList<IUsagePlugin> plugins)
    {
        var supported = plugins.Select(plugin => new AgentDefinition(plugin.Info.Id,
            plugin.Info.DisplayName, plugin.Info.ExecutableNames, plugin.Info.DataSource, plugin.Info.Id));
        return supported.Concat(Known.Where(known => !plugins.Any(plugin =>
            plugin.Info.Id.Equals(known.Id, StringComparison.OrdinalIgnoreCase)))).ToArray();
    }

    public static AgentSelection Select(
        AgentCatalogSnapshot snapshot,
        IReadOnlyList<IUsagePlugin> plugins,
        string? preferredAgentId)
    {
        var supported = snapshot.Agents.Where(agent => agent.IsSupported && agent.IsInstalled).ToArray();
        var preferred = supported.FirstOrDefault(agent =>
            string.Equals(agent.Definition.Id, preferredAgentId, StringComparison.OrdinalIgnoreCase));
        var candidate = preferred ?? supported.FirstOrDefault();
        if (candidate is not null)
        {
            var plugin = plugins.FirstOrDefault(item => item.Info.Id.Equals(
                candidate.Definition.PluginId, StringComparison.OrdinalIgnoreCase));
            if (plugin is not null)
                return new AgentSelection(plugin, candidate,
                    preferred is null ? "First installed supported agent" : "Preferred agent");
        }

        // Keep the Codex adapter available so its repair flow can install the
        // missing dependency and recover on the next refresh.
        var fallback = plugins.FirstOrDefault(item => item.Info.Id.Equals("codex", StringComparison.OrdinalIgnoreCase))
            ?? plugins.First();
        var detectedUnsupported = snapshot.Agents.FirstOrDefault(agent => agent.IsInstalled && !agent.IsSupported);
        return new AgentSelection(fallback, null,
            detectedUnsupported is null
                ? "No installed supported agent; selected adapter can be repaired"
                : $"Detected {detectedUnsupported.Definition.DisplayName}, but no adapter is available");
    }
}

