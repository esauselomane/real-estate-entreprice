using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Reporting;

public static class BrokerReportHelper
{
    public static BrokerPerformanceRow BuildPerformanceRow(BrokerRecord broker, IReadOnlyList<DealRecord> deals)
    {
        var assigned = deals.Where(deal => IsAssigned(deal, broker.BrokerId)).ToList();
        return new(broker.BrokerId, broker.FullName, null, assigned.Count(deal => deal.DealStage == DealWorkflowStage.ClosedWon),
            assigned.Sum(CommissionCalculator.Realised));
    }

    private static bool IsAssigned(DealRecord deal, string brokerId) => deal.AllocatedBrokerIds
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(brokerId, StringComparer.OrdinalIgnoreCase);

    public static BrokerLeaderboardRow BuildLeaderboardRow(IGrouping<string, DealRecord> group, IReadOnlyDictionary<string, string> brokerNames)
    {
        var names = new List<string>();
        foreach (var brokerId in (group.Key ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            names.Add(brokerNames.TryGetValue(brokerId, out var name) ? name : brokerId);
        var label = names.Count == 0 ? "Unassigned" : string.Join(" + ", names);
        return new(label, group.Key ?? string.Empty, group.Count(), Math.Round(group.Sum(deal => deal.TransactionValue), 2),
            group.Sum(CommissionCalculator.Pipeline), group.Count(deal => deal.DealStage == DealWorkflowStage.ClosedWon));
    }

    public static BrokerTargetProgressRow BuildTargetProgress(BrokerMonthlyTarget target, IReadOnlyList<MonthlyCommissionPoint> trend)
    {
        var realised = trend.FirstOrDefault(point => point.Year == target.Year && point.Month == target.Month)?.Gci ?? 0m;
        var progress = target.TargetCommission == 0m ? 0m : Math.Round(realised / target.TargetCommission * 100m, 1);
        return new(target.Year, target.Month, $"{target.Year}-{target.Month:D2}", Math.Round(target.TargetCommission, 2), realised, progress);
    }

    public static BrokerMonthlyTrendRow BuildTrendRow(MonthlyCommissionPoint point, decimal? companyShare)
    {
        decimal? companyDollar = companyShare.HasValue ? Math.Round(point.Gci * companyShare.Value / 100m, 2) : null;
        return new($"{point.Year}-{point.Month:D2}", point.Gci, companyDollar, point.ClosedCount);
    }
}
