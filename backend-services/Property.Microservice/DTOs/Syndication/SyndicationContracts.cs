namespace Property.Microservice.DTOs.Syndication;

public sealed record ListingSnapshot(int PropertyId, string TenantId, string Address, string Description,
    bool ForSale, bool ForRent, decimal? SalePrice, decimal Latitude, decimal Longitude, string[] PhotoUrls);
public sealed record ProviderDescriptor(string Key, string Name, bool Ready, string SetupMessage,
    bool SupportsWithdrawal, bool SupportsStatusChecks, IReadOnlyList<ProviderSetting>? Settings = null);
public sealed record PublicationRequest(bool Publish);
public sealed record ConnectionRequest(string Environment, string BranchId, string AgentId, bool Enabled, Dictionary<string, string>? Settings = null);
public sealed record ProviderResult(string State, string ExternalId = "", string PublicUrl = "", string? Error = null, bool RetrySafe = false);

public sealed record ProviderSetting(string Key, string Label, bool Required = false);
