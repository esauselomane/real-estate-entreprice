using Microsoft.AspNetCore.DataProtection;
using Property.Microservice.Models.Syndication;

namespace Property.Microservice.Services.Syndication;

/// <summary>Encrypts agency secrets with a purpose bound to the agency, provider and environment.</summary>
public sealed class AgencyApiKeyProtector(IDataProtectionProvider provider, IConfiguration configuration, IHostEnvironment environment)
{
    public string Protect(SyndicationConnection connection, string apiKey)
    {
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuration["Syndication:DataProtectionKeysPath"]))
            throw new InvalidOperationException("Secure credential storage needs a persistent Data Protection key directory configured by the platform administrator.");
        return CreateProtector(connection).Protect(apiKey);
    }

    public string Unprotect(SyndicationConnection connection) => CreateProtector(connection).Unprotect(connection.ProtectedApiKey);

    private IDataProtector CreateProtector(SyndicationConnection connection) =>
        provider.CreateProtector("AgencySyndicationKeys.v1", connection.TenantId, connection.ProviderKey, connection.Environment);
}
