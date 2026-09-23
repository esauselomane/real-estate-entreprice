using Property.Microservice.Models.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Resolves server-side credentials for an agency connection. Values must never be returned to browsers or logged.</summary>
public interface IProviderCredentialSource
{
    IReadOnlyDictionary<string, string> Resolve(SyndicationConnection connection);
}
