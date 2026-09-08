namespace Shared.Kafka.Events;

/// <summary>
///   Published by Property.Microservice when brochure generation is requested.
///   Consumed by BrochureGeneration.Worker.
/// </summary>
public record BrochureGenerationRequestedEvent(
    int DealId,
    int BrochureCardId,
    int PropertyId,
    string TenantId,
    string RequestedBy,
    string BrokerName,
    string BrokerEmail,
    string BrokerPhone,
    string CompanyName,
    string? CompanyLogoUrl,
    string ClientName,
    string ClientEmail,
    string ClientPhone,
    string MarketClass,
    decimal TenantInstallationAllowance,
    List<BrochureSubUnitEvent> SubUnits
);

/// <summary>
///   A single unit row inside a brochure generation request.
/// </summary>
public record BrochureSubUnitEvent
{
    public int PropertyId { get; init; }
    public string PropertyDescription { get; init; } = string.Empty;
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public string UnitNumber { get; init; } = string.Empty;
    public string UnitDescription { get; init; } = string.Empty;
    public decimal NetRentableAreaSqM { get; init; }
    public decimal? RentalRate { get; init; }
    public decimal? RentEscalationPercentage { get; init; }
    public int? LeaseTermMonths { get; init; }
    public decimal? SalePrice { get; init; }
    public List<string> SelectedImageRefs { get; init; } = new();
}

/// <summary>
///   Published by BrochureGeneration.Worker when generation completes.
///   Consumed by CRM.Microservice (notification + BrochureCard update) and
///   Property.Microservice.
/// </summary>
public record BrochureGenerationCompletedEvent(
    int DealId,
    int BrochureCardId,
    int PropertyId,
    string TenantId,
    string RequestedBy,
    bool Success,
    string? PdfUrl,
    string? ErrorMessage
);
