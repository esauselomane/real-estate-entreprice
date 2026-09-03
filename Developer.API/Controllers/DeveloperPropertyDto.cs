namespace Developer.API.Controllers
{
    public class DeveloperPropertyDto
    {
        public int PropertyId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string CompleteAddress { get; set; } = string.Empty;
        public string ErfSpatialSize { get; set; } = string.Empty;
        public string TitleDeedType { get; set; } = string.Empty;
        public string PortfolioZoning { get; set; } = string.Empty;
        public string MarketClass { get; set; } = string.Empty;
        public bool IsForSale { get; set; }
        public bool IsForRent { get; set; }
        public int PropertyTypeId { get; set; }
        public decimal ValuationPrice { get; set; }
        public decimal? SalePrice { get; set; }
        public decimal TenantInstallationAllowance { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string LinkedOwnerId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
