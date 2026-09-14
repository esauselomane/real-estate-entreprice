using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Computes entitlement from persisted dates without trusting browser redirects or stale JWT trial claims.</summary>
public static class PaymentStateHelper
{
    public static SubscriptionStatusResponse Describe(TenantAgency tenant, Subscription? subscription, PricingPackage? package, DateTime now)
    {
        var status = subscription?.Status ?? (tenant.IsTrial ? SubscriptionStatus.Trialing : SubscriptionStatus.Expired);
        var end = subscription?.CurrentPeriodEnd ?? tenant.TrialEndDate ?? now;
        var trialEnd = subscription?.TrialEndsAt ?? tenant.TrialEndDate;
        if (status == SubscriptionStatus.Trialing)
            end = trialEnd ?? end;
        if (end <= now && status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.Cancelled)
            status = SubscriptionStatus.Expired;
        var hasAccess = end > now && status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.Cancelled;
        return new SubscriptionStatusResponse
        {
            Status = status.ToString().ToLowerInvariant(),
            PricingPackageCode = subscription?.PricingPackageCode ?? "NONE",
            PricingPackageName = package?.Name ?? subscription?.PricingPackageCode ?? "Free trial",
            MonthlyPrice = package?.MonthlyPrice,
            AnnualPrice = package?.AnnualPrice,
            CurrentPeriodStart = subscription?.CurrentPeriodStart ?? tenant.TrialStartDate ?? now,
            CurrentPeriodEnd = end,
            TrialEndsAt = trialEnd,
            DaysUntilExpiry = Math.Max(0, (int)Math.Ceiling((end - now).TotalDays)),
            IsTrial = status == SubscriptionStatus.Trialing,
            CanUpgrade = status != SubscriptionStatus.Active,
            RequiresUpgrade = !hasAccess,
            CanManageBilling = subscription?.Gateway != null && subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue or SubscriptionStatus.Paused,
            Gateway = subscription?.Gateway
        };
    }
}
