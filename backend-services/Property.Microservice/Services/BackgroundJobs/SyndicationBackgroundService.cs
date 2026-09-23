using Property.Microservice.Services.Syndication;

namespace Property.Microservice.Services.BackgroundJobs;

/// <summary>Polls the durable syndication outbox; jobs survive service restarts.</summary>
public sealed class SyndicationBackgroundService(IServiceScopeFactory scopes, ILogger<SyndicationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<SyndicationProcessor>();
                await processor.ProcessNextAsync(stoppingToken);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError("Syndication processing failed; committed pending work remains queued.");
            }
        }
    }
}
