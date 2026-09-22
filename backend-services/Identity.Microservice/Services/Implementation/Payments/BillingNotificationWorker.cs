using System.Data;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Delivers committed notifications with bounded retries and schedules local trial warnings.</summary>
public sealed class BillingNotificationWorker(IServiceScopeFactory scopes, ILogger<BillingNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError(exception, "Billing notification processing failed"); }
        }
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IBillingNotificationEmailSender>();
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await database.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource='billing-notifications', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=1000; IF @result < 0 THROW 50001, 'Notifications busy', 1;", cancellationToken);
        var now = DateTime.UtcNow;
        var soon = now.AddDays(3);
        var trials = await database.TenantAgencies.Where(tenant => tenant.IsTrial && tenant.TrialEndDate > now && tenant.TrialEndDate <= soon).ToListAsync(cancellationToken);
        foreach (var tenant in trials)
        {
            var key = $"trial:{tenant.TenantId}:{tenant.TrialEndDate:yyyyMMdd}";
            if (!await database.BillingNotifications.AnyAsync(item => item.EventKey == key, cancellationToken))
                database.BillingNotifications.Add(new BillingNotification { EventKey = key, Recipient = tenant.ContactEmail, Template = "trial-ending" });
        }
        await database.SaveChangesAsync(cancellationToken);
        var pending = await database.BillingNotifications.Where(item => item.SentAt == null && item.Attempts < 5 && (item.NextAttemptAt == null || item.NextAttemptAt <= now)).Take(20).ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            try
            {
                await emailSender.SendAsync(item.Recipient, item.Template, cancellationToken);
                item.SentAt = now;
                item.Error = null;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                item.Error = exception.Message;
                item.NextAttemptAt = now.AddMinutes(Math.Pow(2, item.Attempts + 1));
            }
            item.Attempts++;
        }
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
