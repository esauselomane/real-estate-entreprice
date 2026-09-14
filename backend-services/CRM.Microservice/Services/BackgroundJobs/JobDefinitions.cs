using System.Text.Json.Serialization;

namespace CRM.Microservice.Services.BackgroundJobs
{
    /// <summary>
    /// Brochure generation job - replaces BrochureGenerationRequestedEvent from Kafka.
    /// </summary>
    public record BrochureGenerationJob(
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
        List<BrochureSubUnitJob> SubUnits
    );

    public record BrochureSubUnitJob
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

        public string? PropertyType { get; init; }
        public decimal? ValuationPrice { get; init; }
        public string? ErfSpatialSize { get; init; }
        public string? TitleDeedType { get; init; }
        public string? PortfolioZoning { get; init; }
        public bool IsForSale { get; init; }
        public bool IsForRent { get; init; }
        public bool IsCurrentlyVacant { get; init; }
        public List<AmenityInfo> Amenities { get; init; } = new();
    }

    public record AmenityInfo
    {
        public string Name { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public bool BooleanValue { get; init; }
        public int? NumberValue { get; init; }
    }

    /// <summary>
    /// Result of brochure generation job.
    /// </summary>
    public record BrochureGenerationResult(
        bool Success,
        string? PdfPath,
        string? ErrorMessage
    );

    /// <summary>
    /// Data import job - replaces DataImport Kafka events.
    /// </summary>
    public record DataImportJob(
        string JobId,
        string ImportType,
        string FileName,
        string Base64Content,
        string TenantId,
        string RequestedBy
    );

    public record DataImportResult(
        string Status,
        string Message,
        int SuccessCount,
        int ErrorCount,
        List<string> Errors
    );

    /// <summary>
    /// Notification job - replaces NotificationCreatedEvent from Kafka.
    /// </summary>
    public record NotificationJob(
        string TenantId,
        string UserId,
        string Category,
        string Title,
        string Body,
        string LinkType,
        string LinkId
    );
}