using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;

namespace Property.Microservice.Services.PublicListings;

/// <summary>Withdraws expired rental-only listings without changing operational letting flags or sale listings.</summary>
public sealed class PublicListingExpiryService(IServiceScopeFactory scopes, ILogger<PublicListingExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();
                var now = DateTime.UtcNow;
                var count = await database.PropertyAssets.Where(property => property.IsPublicListing && property.IsForRent && !property.IsForSale && property.PublicLettingExpiresAt <= now)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(property => property.IsPublicListing, false).SetProperty(property => property.PublicReviewStatus, "Expired"), stoppingToken);
                if (count > 0) logger.LogInformation("Withdrew {Count} expired letting listings", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Could not process expired public listings"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
