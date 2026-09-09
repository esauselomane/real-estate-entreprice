using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
    public record ContactSearchDto
    {
        public int Page { get; set; } = 1;
        public string? TenantId { get; set; }
        public string? ContactType { get; set; }
        public decimal? BudgetMin { get; set; }
        public decimal? BudgetMax { get; set; }
        public List<int>? AreaIds { get; set; }
        public string? Area { get; set; }
    }
}
