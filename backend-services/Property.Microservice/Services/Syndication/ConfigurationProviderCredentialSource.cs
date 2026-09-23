using Property.Microservice.Models.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Reads secrets injected by the host's configuration providers, isolated by immutable connection ID.</summary>
public sealed class ConfigurationProviderCredentialSource(IConfiguration configuration, AgencyApiKeyProtector? protector = null) : IProviderCredentialSource
{
    public IReadOnlyDictionary<string, string> Resolve(SyndicationConnection connection)
    {
        var section = configuration.GetSection($"Syndication:ConnectionCredentials:{connection.Id}");
        var credentials = section.GetChildren().Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key, item => item.Value!, StringComparer.OrdinalIgnoreCase);
        if (connection.HasApiKey)
        {
            if (protector == null) throw new InvalidOperationException("Credential protection is unavailable.");
            credentials["ApiKey"] = protector.Unprotect(connection);
        }
        if (credentials.Count == 0) throw new InvalidOperationException("Portal credentials have not been configured for this agency connection.");
        return credentials;
    }
}
