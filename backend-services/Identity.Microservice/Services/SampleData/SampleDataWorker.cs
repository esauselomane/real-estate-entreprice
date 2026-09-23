using Identity.Microservice.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Services.SampleData;

/// <summary>Claims only administrator-approved jobs, with a recoverable lease and bounded retries.</summary>
public sealed class SampleDataWorker(IServiceScopeFactory scopes, ILogger<SampleDataWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunNextAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning("Sample data worker unavailable ({ExceptionType}).", exception.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    public async Task RunNextAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var now = DateTime.UtcNow;
        var tenantId = await database.SampleDataJobs.AsNoTracking()
            .Where(item => item.ApprovedBy != null && (item.State == "Pending" || item.State == "Processing") && item.AvailableAt <= now)
            .OrderBy(item => item.AvailableAt).Select(item => item.TenantId).FirstOrDefaultAsync(cancellationToken);
        if (tenantId == null) return;
        var claimed = await database.SampleDataJobs
            .Where(item => item.TenantId == tenantId && item.AvailableAt <= now && (item.State == "Pending" || item.State == "Processing"))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "Processing")
                .SetProperty(item => item.AvailableAt, now.AddMinutes(10))
                .SetProperty(item => item.Attempts, item => item.Attempts + 1), cancellationToken);
        if (claimed == 0) return;
        var job = await database.SampleDataJobs.SingleAsync(item => item.TenantId == tenantId, cancellationToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(4));
        try { await scope.ServiceProvider.GetRequiredService<SampleDataProcessor>().ProcessAsync(job, timeout.Token); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            job.State = job.Attempts >= 3 ? "Failed" : "Pending";
            job.AvailableAt = DateTime.UtcNow.AddMinutes(1);
            job.Error = job.ProvisionedAt.HasValue
                ? "Sample data is ready, but the notification email could not be sent. Retry to send it."
                : "Provisioning could not finish. Check service configuration and availability, then retry.";
            await database.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Sample data job for {TenantId} failed ({ExceptionType}).", tenantId, exception.GetType().Name);
        }
    }
}
