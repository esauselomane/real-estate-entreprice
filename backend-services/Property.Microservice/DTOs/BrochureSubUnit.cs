using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
    public record class BrochureSubUnit
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

    public record class AmenityInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool BooleanValue { get; set; }
        public int? NumberValue { get; set; }
    }
}
