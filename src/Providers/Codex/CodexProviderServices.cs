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
        return services;
    }
}
