using Property.Microservice.DTOs.Syndication;
using Property.Microservice.Models.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Provider boundary. Implementations own authentication, mapping, validation and reconciliation.
/// Submitted means accepted, never live. RetrySafe may only be true when repeating the operation cannot duplicate a listing.</summary>
public interface IListingProvider
{
    ProviderDescriptor Descriptor { get; }
    Task<IReadOnlyList<string>> ValidateAsync(ListingSnapshot listing, SyndicationConnection connection, CancellationToken cancellationToken);
    Task<ProviderResult> ExecuteAsync(string operation, ListingSnapshot listing, SyndicationConnection connection,
        string externalId, string requestKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> TestConnectionAsync(SyndicationConnection connection, CancellationToken cancellationToken);
}
