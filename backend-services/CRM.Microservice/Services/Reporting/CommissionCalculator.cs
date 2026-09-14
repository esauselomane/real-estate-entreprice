using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Reporting;

/// <summary>Canonical per-deal currency calculations. Explicit zero realised commission is preserved.</summary>
public static class CommissionCalculator
{
    public static decimal Expected(DealRecord deal) => Math.Round(deal.CommissionType == CommissionTypes.Percent
        ? deal.TransactionValue * deal.CommissionValue / 100m : deal.CommissionValue, 2);
    public static decimal Realised(DealRecord deal) => deal.DealStage == DealWorkflowStage.ClosedWon
        ? Math.Round(deal.RealisedCommission ?? Expected(deal), 2) : 0m;
    public static decimal Pipeline(DealRecord deal) => deal.DealStage switch
    {
        DealWorkflowStage.ClosedWon => Realised(deal),
        DealWorkflowStage.ClosedLost => 0m,
        _ => Expected(deal)
    };
}
