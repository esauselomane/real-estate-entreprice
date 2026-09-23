namespace Property.Microservice.Services.Syndication;

/// <summary>Resolves providers by stable keys without provider branches in the publishing workflow.</summary>
public sealed class ListingProviderRegistry(IEnumerable<IListingProvider> providers)
{
    private readonly IReadOnlyDictionary<string, IListingProvider> registered = providers.ToDictionary(provider => provider.Descriptor.Key, StringComparer.OrdinalIgnoreCase);
    public IEnumerable<IListingProvider> All => registered.Values;
    public IListingProvider Get(string key) => registered.TryGetValue(key, out var provider)
        ? provider : throw new ArgumentException("This listing provider is not installed.");
}
