using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;

namespace CRM.Microservice.Services.Implementation
{
    /// <summary>
    /// Background service that computes broker performance metrics once daily
    /// during off-peak hours. Results are persisted to the
    /// BrokerPerformanceMetrics table for read-time suggestion queries.
    /// </summary>
    public class BrokerMetricsBackgroundJob : BackgroundService
    {
        private readonly ILogger<BrokerMetricsBackgroundJob> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public BrokerMetricsBackgroundJob(
            ILogger<BrokerMetricsBackgroundJob> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = _configuration.GetValue<bool>("BrokerScoring:BackgroundJob:Enabled", true);
            if (!enabled)
            {
                _logger.LogInformation("BrokerMetricsBackgroundJob is disabled. Skipping startup.");
                return;
            }

            _logger.LogInformation("BrokerMetricsBackgroundJob started — running daily at 02:00 UTC.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunComputationCycle(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BrokerMetricsBackgroundJob encountered an error during computation cycle.");
                }

                var delay = TimeSpan.FromHours(24);
                await Task.Delay(delay, stoppingToken);
            }
        }

        private async Task RunComputationCycle(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var scoreService = scope.ServiceProvider.GetRequiredService<IBrokerScoreService>();
            var tenantRepo = scope.ServiceProvider.GetRequiredService<IAgencyRepository>();
            var metricsRepo = scope.ServiceProvider.GetRequiredService<IBrokerPerformanceMetricsRepository>();

            var tenants = await tenantRepo.Query()
                .AsNoTracking()
                .Select(a => a.TenantId)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var tenantId in tenants)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var metrics = await scoreService.ComputeAllBrokerMetricsAsync(tenantId);
                    if (metrics.Any())
                    {
                        var metricDate = metrics.First().MetricDate;
                        await metricsRepo.UpsertMetricsAsync(tenantId, metricDate, metrics);
                        _logger.LogInformation(
                            "Computed {Count} broker metrics for tenant {TenantId} on {MetricDate}.",
                            metrics.Count, tenantId, metricDate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compute broker metrics for tenant {TenantId}.", tenantId);
                }
            }
        }
    }
}
