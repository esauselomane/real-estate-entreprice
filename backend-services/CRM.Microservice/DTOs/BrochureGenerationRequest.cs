using System.Collections.Generic;

namespace CRM.Microservice.DTOs
{
    public class BrochureGenerationRequest
    {
        public int DealId { get; set; }
        public int BrochureCardId { get; set; }
        public int PropertyId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public string BrokerName { get; set; } = string.Empty;
        public string BrokerEmail { get; set; } = string.Empty;
        public string BrokerPhone { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? CompanyLogoUrl { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string MarketClass { get; set; } = string.Empty;
        public decimal TenantInstallationAllowance { get; set; }
        public List<BrochureSubUnitRequest> SubUnits { get; set; } = new();
    }

    public class BrochureSubUnitRequest
    {
        public int PropertyId { get; set; }
        public string PropertyDescription { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public string UnitDescription { get; set; } = string.Empty;
        public decimal NetRentableAreaSqM { get; set; }
        public decimal? RentalRate { get; set; }
        public decimal? RentEscalationPercentage { get; set; }
        public int? LeaseTermMonths { get; set; }
        public decimal? SalePrice { get; set; }
        public List<string> SelectedImageRefs { get; set; } = new();

        public string? PropertyType { get; set; }
        public decimal? ValuationPrice { get; set; }
        public string? ErfSpatialSize { get; set; }
        public string? TitleDeedType { get; set; }
        public string? PortfolioZoning { get; set; }
        public bool IsForSale { get; set; }
        public bool IsForRent { get; set; }
        public bool IsCurrentlyVacant { get; set; }
        public List<AmenityInfo> Amenities { get; set; } = new();
    }

    public class AmenityInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool BooleanValue { get; set; }
        public int? NumberValue { get; set; }
    }
}