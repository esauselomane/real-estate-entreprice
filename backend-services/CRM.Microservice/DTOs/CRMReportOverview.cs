using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs
{
    public record class CRMReportOverview
    {
        public int TotalLeads { get; set; }
        public int TotalDeals { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalRealisedCommission { get; set; }
        public List<ReportDrilldownRow> MonthlyBreakdown { get; set; } = new();
    }
}
