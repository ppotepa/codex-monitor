using Microsoft.Extensions.DependencyInjection;
using CodexMonitor.Core.Workspace;

namespace CodexMonitor.Providers.Codex;

public static class CodexProviderServices
{
    public static IServiceCollection AddCodexProvider(this IServiceCollection services)
    {
        services.AddSingleton<IUsageProvider, CodexProvider>();
        services.AddSingleton<IInstructionsOptimizer, CodexInstructionsOptimizer>();
        services.AddSingleton<IAgentSessionSource, CodexAppServerSessionSource>();
        services.AddSingleton<IWorkspaceAiPlanner, CodexWorkspaceAiPlanner>();
        services.AddSingleton<IFeature>(sp => sp.GetRequiredService<IInstructionsOptimizer>());
        services.AddSingleton<IFeature>(sp => sp.GetRequiredService<IAgentSessionSource>());
        services.AddSingleton<IFeature>(sp => sp.GetRequiredService<IWorkspaceAiPlanner>());
        services.AddSingleton<IFeature>(sp => new LimitsFeatureAdapter(
            sp.GetServices<IUsageProvider>().Single(provider => provider.Info.Id == "codex")));
        services.AddSingleton<IFeature>(sp => new TokenUsageFeatureAdapter("codex",
            (ITokenUsageProvider)sp.GetServices<IUsageProvider>().Single(provider => provider.Info.Id == "codex")));
        services.AddSingleton<IFeature>(sp => new DependencyRepairFeatureAdapter("codex",
            (IRepairableUsageProvider)sp.GetServices<IUsageProvider>().Single(provider => provider.Info.Id == "codex")));
        return services;
    }
}
