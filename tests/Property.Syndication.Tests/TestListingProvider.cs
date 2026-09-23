using Property.Microservice.DTOs.Syndication;
using Property.Microservice.Models.Syndication;
using Property.Microservice.Services.Syndication;

namespace Property.Syndication.Tests;

internal sealed class TestListingProvider(string key = "test-provider") : IListingProvider
{
    public ProviderDescriptor Descriptor => new(key, key, true, "", true, true);
    public List<string> Operations { get; } = [];
    public List<string> RequestKeys { get; } = [];
    public ProviderResult Result { get; set; } = new("Live", "external-1", "https://portal.example/listing/1");
    public bool Throw { get; set; }
    public Task<IReadOnlyList<string>> ValidateAsync(ListingSnapshot listing, SyndicationConnection connection, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IReadOnlyList<string>> TestConnectionAsync(SyndicationConnection connection, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<ProviderResult> ExecuteAsync(string operation, ListingSnapshot listing, SyndicationConnection connection, string externalId, string requestKey, CancellationToken cancellationToken)
    {
        Operations.Add(operation);
        RequestKeys.Add(requestKey);
        if (Throw) throw new HttpRequestException("sensitive provider details");
        return Task.FromResult(Result);
    }
}
