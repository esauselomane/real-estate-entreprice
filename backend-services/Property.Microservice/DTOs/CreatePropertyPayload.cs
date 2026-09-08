using Property.Microservice.Models;

namespace Property.Microservice.DTOs
{
        public record class CreatePropertyPayload
        {
            public string CompleteAddress { get; set; } = string.Empty;
            public string ErfSpatialSize { get; set; } = string.Empty;
            public string TitleDeedType { get; set; } = "Freehold";
            public string PortfolioZoning { get; set; } = "Commercial Zone";
            public string MarketClass { get; set; } = "Sale";
            public int PropertyTypeId { get; set; } = 1;
            public decimal ValuationPrice { get; set; }
            public decimal? SalePrice { get; set; }
            public decimal TenantInstallationAllowance { get; set; } = 0;
            public decimal Latitude { get; set; } = -33.9249m;
            public decimal Longitude { get; set; } = 18.4241m;
            public string LinkedOwnerId { get; set; } = "OT-0001";
            // 🧬 Independent listing eligibility flags — a property may be marketed
            // for SALE and/or RENT at the same time. If both are null/false the
            // backend infers them from MarketClass for backward compatibility.
            public bool? IsForSale { get; set; }
            public bool? IsForRent { get; set; }
            public List<CreatePropertyUnitPayload> SubUnits { get; set; } = new List<CreatePropertyUnitPayload>();
        }
}
