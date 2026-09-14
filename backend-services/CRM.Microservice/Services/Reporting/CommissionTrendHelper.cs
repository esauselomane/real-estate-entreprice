using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Reporting;

public static class CommissionTrendHelper
{
    public static List<MonthlyCommissionPoint> Build(IReadOnlyList<DealRecord> deals, DateTime start, int months)
    {
        var points = new List<MonthlyCommissionPoint>();
        for (var index = 0; index < months; index++)
        {
            var month = new DateTime(start.Year, start.Month, 1).AddMonths(index);
            var end = month.AddMonths(1);
            var closed = deals.Where(deal => deal.DealStage == DealWorkflowStage.ClosedWon && deal.LastStageUpdatedAt >= month && deal.LastStageUpdatedAt < end).ToList();
            points.Add(new(month.Year, month.Month, closed.Sum(CommissionCalculator.Realised), closed.Count));
        }
        return points;
    }
}
