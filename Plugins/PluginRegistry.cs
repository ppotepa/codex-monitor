namespace CodexMonitor;

internal static class PluginRegistry
{
    // Compiled-in plugins only. No assembly scanning or downloads on startup.
    public static IReadOnlyList<IUsagePlugin> All { get; } =
        Array.AsReadOnly<IUsagePlugin>([new Plugins.Codex.CodexPlugin()]);

    public static IUsagePlugin Get(string id) => All.SingleOrDefault(plugin =>
        plugin.Info.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"The '{id}' plugin is not supported.");
}
