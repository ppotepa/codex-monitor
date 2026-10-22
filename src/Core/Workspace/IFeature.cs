namespace CodexMonitor.Core.Workspace;

// Capabilities are provider-owned adapters. Application features (Git, Goals, TTS) are separate.
public interface IFeature
{
    string ProviderId { get; }
    string FeatureId { get; }
    int Priority { get; }
}

public sealed record FeatureSupport(string ProviderId, string FeatureId, bool Implemented);

public interface IFeatureRegistry
{
    IReadOnlyList<T> Candidates<T>(string providerId) where T : class, IFeature;
    FeatureSupport Describe(string providerId, string featureId);
}

public sealed class FeatureRegistry(IEnumerable<IFeature> features) : IFeatureRegistry
{
    private readonly IReadOnlyList<IFeature> _features = features.ToArray();

    public IReadOnlyList<T> Candidates<T>(string providerId) where T : class, IFeature =>
        _features.OfType<T>()
            .Where(feature => string.Equals(feature.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(feature => feature.Priority)
            .ToArray();

    public FeatureSupport Describe(string providerId, string featureId) =>
        new(providerId, featureId, _features.Any(feature =>
            string.Equals(feature.ProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(feature.FeatureId, featureId, StringComparison.OrdinalIgnoreCase)));
}
