using Identity.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Data;

/// <summary>Applies the existing-trial concession once without resetting paid tenants or new-trial dates.</summary>
public static class TrialExtensionHelper
{
    public static async Task ApplyAsync(IdentityDbContext context)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (context.Database.IsSqlServer())
            await context.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource='trial-extension', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 50001, 'Trial extension busy', 1;");
        var now = DateTime.UtcNow;
        var tenants = await context.TenantAgencies.Where(tenant => tenant.IsTrial && tenant.TrialExtensionAppliedAt == null).ToListAsync();
        foreach (var tenant in tenants)
        {
            var subscriptions = await context.Subscriptions.Where(subscription => subscription.TenantId == tenant.TenantId).ToListAsync();
            if (subscriptions.Any(subscription => subscription.Status == SubscriptionStatus.Active))
                continue;
            var extendedEnd = (tenant.TrialEndDate ?? now).AddMonths(2);
            tenant.TrialEndDate = extendedEnd;
            tenant.TrialExtensionAppliedAt = now;
            tenant.SubscriptionEndsAt = extendedEnd;
            foreach (var subscription in subscriptions.Where(subscription => subscription.Status == SubscriptionStatus.Trialing))
            {
                subscription.TrialEndsAt = extendedEnd;
                subscription.CurrentPeriodEnd = extendedEnd;
            }
            var users = await context.UserAccounts.Where(user => user.TenantId == tenant.TenantId && user.IsTrial).ToListAsync();
            foreach (var user in users)
                user.TrialEndDate = extendedEnd;
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
