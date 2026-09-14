using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Reporting;

public static class ReportSummaryHelper
{
    public static CRMReportOverview Build(int year, IReadOnlyList<LeadRecord> leads, IReadOnlyList<DealRecord> deals)
    {
        var monthly = new List<ReportDrilldownRow>();
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);
            monthly.Add(new ReportDrilldownRow
            {
                Period = start.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
                Leads = leads.Count(lead => lead.CreatedAt >= start && lead.CreatedAt < end),
                Deals = deals.Count(deal => deal.CreatedAt >= start && deal.CreatedAt < end),
                Revenue = deals.Where(deal => deal.LastStageUpdatedAt >= start && deal.LastStageUpdatedAt < end).Sum(CommissionCalculator.Realised)
            });
        }
        return new CRMReportOverview
        {
            TotalLeads = monthly.Sum(row => row.Leads), TotalDeals = monthly.Sum(row => row.Deals),
            TotalRevenue = monthly.Sum(row => row.Revenue), TotalRealisedCommission = monthly.Sum(row => row.Revenue), MonthlyBreakdown = monthly
        };
    }
}
