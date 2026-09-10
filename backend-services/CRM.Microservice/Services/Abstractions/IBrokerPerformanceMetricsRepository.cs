using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions
{
    /// <summary>
    /// Repository contract for broker performance metrics, persisting computed
    /// scores to the BrokerPerformanceMetrics table for read-time suggestion queries.
    /// </summary>
    public interface IBrokerPerformanceMetricsRepository : IRepository<BrokerPerformanceMetrics>
    {
        /// <summary>
        /// Returns the most recent metrics row for each broker in the tenant.
        /// </summary>
        Task<List<BrokerPerformanceDto>> GetLatestMetricsForTenantAsync(string tenantId);

        /// <summary>
        /// Returns the latest metrics for brokers across multiple tenants.
        /// </summary>
        Task<List<BrokerPerformanceDto>> GetLatestMetricsForTenantsAsync(List<string> tenantIds);

        /// <summary>
        /// Replaces all metrics for a given tenant and date with the provided list.
        /// </summary>
        Task UpsertMetricsAsync(string tenantId, DateTime metricDate, List<BrokerPerformanceMetrics> metrics);
    }
}
