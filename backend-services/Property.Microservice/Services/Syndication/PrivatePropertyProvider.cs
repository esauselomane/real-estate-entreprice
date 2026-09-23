using Property.Microservice.DTOs.Syndication;
using Property.Microservice.Models.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Private Property integration boundary. Transmission is deliberately disabled until its
/// security-token signing and response semantics are supplied and verified against the sandbox.</summary>
public sealed class PrivatePropertyProvider : IListingProvider
{
    private const string MissingGuide = "Private Property publishing is not enabled yet. The provider authentication guide and sandbox branch/agent IDs are required.";
    public ProviderDescriptor Descriptor => new("private-property", "Private Property", false, MissingGuide, true, true);
    public Task<IReadOnlyList<string>> ValidateAsync(ListingSnapshot listing, SyndicationConnection connection, CancellationToken cancellationToken)
    {
        var errors = new List<string> { MissingGuide };
        if (!Guid.TryParse(connection.BranchId, out _)) errors.Add("Map a valid Private Property branch ID.");
        if (string.IsNullOrWhiteSpace(connection.AgentId)) errors.Add("Map a Private Property agent ID.");
        if (string.IsNullOrWhiteSpace(listing.Description)) errors.Add("Add a listing description.");
        if (listing.PhotoUrls.Length == 0) errors.Add("Add publicly accessible listing photos.");
        return Task.FromResult<IReadOnlyList<string>>(errors);
    }
    public Task<ProviderResult> ExecuteAsync(string operation, ListingSnapshot listing, SyndicationConnection connection,
        string externalId, string requestKey, CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderResult("NeedsAttention", Error: MissingGuide));
    public Task<IReadOnlyList<string>> TestConnectionAsync(SyndicationConnection connection, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([MissingGuide]);
}
