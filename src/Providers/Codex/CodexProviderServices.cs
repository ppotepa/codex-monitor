using Microsoft.Extensions.DependencyInjection;

namespace CodexMonitor.Providers.Codex;

public static class CodexProviderServices
{
    public static IServiceCollection AddCodexProvider(this IServiceCollection services)
    {
        services.AddSingleton<IUsageProvider, CodexProvider>();
        return services;
    }
}
