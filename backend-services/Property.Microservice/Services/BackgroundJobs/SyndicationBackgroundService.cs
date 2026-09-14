using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Property.Microservice.Services.BackgroundJobs
{
    public class SyndicationBackgroundService : BackgroundService
    {
        private readonly ILogger<SyndicationBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IJobQueue<SyndicationJob> _jobQueue;

        public SyndicationBackgroundService(
            ILogger<SyndicationBackgroundService> logger,
            IServiceScopeFactory scopeFactory,
            IJobQueue<SyndicationJob> jobQueue)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _jobQueue = jobQueue;
        }

        public async ValueTask EnqueueAsync(SyndicationJob job, CancellationToken ct = default)
        {
            await _jobQueue.EnqueueAsync(job, ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[SYNDICATION BACKGROUND SERVICE]: Started, listening for syndication jobs...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _jobQueue.DequeueAsync(stoppingToken);
                    string platformName = job.PlatformId == 1 ? "Property24" : (job.PlatformId == 2 ? "PrivateProperty" : "Gumtree");

                    _logger.LogInformation("[SYNDICATION]: Preparing payload for {PlatformName}, PropertyId={PropertyId}", platformName, job.PropertyId);

                    var result = await ExecuteExternalPortalTransmissionAsync(job, stoppingToken);

                    // Update syndication record in DB
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<PropertyDbContext>();

                    var syndication = await dbContext.PropertyPlatformSyndications
                        .FirstOrDefaultAsync(s => s.PropertyId == job.PropertyId && s.PlatformId == job.PlatformId, stoppingToken);

                    if (syndication != null)
                    {
                        syndication.IsSyndicatedActive = result.Success;
                        syndication.ExternalReferenceIndex = result.ExternalListingId;
                        syndication.LastSynchronizedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }

                    if (result.Success)
                    {
                        _logger.LogInformation("[SYNC SUCCESS] Property {PropertyId} listed on {PlatformName} with ExternalId={ExternalId}", 
                            job.PropertyId, platformName, result.ExternalListingId);
                    }
                    else
                    {
                        _logger.LogInformation("[DE-LIST SUCCESS] Property {PropertyId} removed from {PlatformName}", 
                            job.PropertyId, platformName);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SYNDICATION BACKGROUND SERVICE]: Error processing job");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task<SyndicationResult> ExecuteExternalPortalTransmissionAsync(SyndicationJob evt, CancellationToken ct)
        {
            await Task.Delay(1500, ct); // Simulate external API call

            string externalListingId = evt.PlatformId == 1 ? $"P24-{evt.PropertyId}" 
                : (evt.PlatformId == 2 ? $"PP-{evt.PropertyId}" : $"GT-{evt.PropertyId}");

            return new SyndicationResult(
                PropertyId: evt.PropertyId,
                PlatformId: evt.PlatformId,
                Success: evt.IsActive,
                ExternalListingId: externalListingId,
                TenantId: evt.TenantId
            );
        }
    }
}