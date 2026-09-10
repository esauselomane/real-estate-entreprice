using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions
{
    /// <summary>
    /// Computes broker performance metrics and provides broker suggestion logic
    /// for intelligent lead-to-broker matching.
    /// </summary>
    public interface IBrokerScoreService
    {
        /// <summary>
        /// Computes performance metrics for all brokers in a tenant.
        /// </summary>
        Task<List<BrokerPerformanceMetrics>> ComputeAllBrokerMetricsAsync(string tenantId);

        /// <summary>
        /// Returns the top 3 suggested brokers for a new lead, weighted by
        /// conversion rate, geographic and property-type specialization,
        /// and price-band competency.
        /// </summary>
        Task<List<SuggestedBrokerDto>> GetSuggestedBrokersAsync(
            string tenantId,
            decimal? budgetMin,
            decimal? budgetMax,
            string? propertyType,
            string? area,
            string currentUserId = "");
    }
}
