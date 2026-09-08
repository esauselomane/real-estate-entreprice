namespace Shared.Kafka.Events;

/// <summary>
///   Published by Property.Microservice when a property should be syndicated.
///   Consumed by PlatformSyndication.Worker.
/// </summary>
public record PropertySyndicationRequestedEvent(
    int PropertyId,
    int PlatformId,
    bool IsActive,
    string TenantId,
    string RequestedBy
);

/// <summary>
///   Published by PlatformSyndication.Worker when syndication completes.
///   Consumed by Property.Microservice.
/// </summary>
public record PropertySyndicationCompletedEvent(
    int PropertyId,
    int PlatformId,
    bool Success,
    string? ExternalListingId,
    string TenantId
);
