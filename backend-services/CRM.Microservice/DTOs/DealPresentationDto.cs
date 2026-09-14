namespace CRM.Microservice.DTOs;

public sealed record DealBrokerContactDto(string BrokerId, string FullName, string Email, string Phone);
public sealed record DealPresentationDto(string TenantId, string AgencyName, string? AgencyLogoUrl, IReadOnlyList<DealBrokerContactDto> Brokers);
