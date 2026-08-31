namespace Identity.Microservice.Controllers
{
    // 🟢 Public DTO — pricing package summary from the marketing website.
    public class PricingPackageDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? MonthlyPrice { get; set; }
        public decimal? AnnualPrice { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
    }
}
