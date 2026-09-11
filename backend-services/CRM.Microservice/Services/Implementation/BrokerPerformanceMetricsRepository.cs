using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;
using CRM.Microservice.DTOs;

namespace CRM.Microservice.Services.Implementation
{
    public class BrokerPerformanceMetricsRepository : Repository<BrokerPerformanceMetrics>, IBrokerPerformanceMetricsRepository
    {
        private readonly CRMDbContext _dbContext;

        public BrokerPerformanceMetricsRepository(CRMDbContext dbContext, ILogger<Repository<BrokerPerformanceMetrics>> logger)
            : base(dbContext, logger)
        {
            _dbContext = dbContext;
        }

        public async Task<List<BrokerPerformanceDto>> GetLatestMetricsForTenantAsync(string tenantId)
        {
            var latestDate = await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => m.TenantId == tenantId)
                .MaxAsync(m => m.MetricDate);

            var rows = await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => m.TenantId == tenantId && m.MetricDate == latestDate)
                .ToListAsync();

            return rows.Select(ToDto).ToList();
        }

        public async Task<List<BrokerPerformanceDto>> GetLatestMetricsForTenantsAsync(List<string> tenantIds)
        {
            var latestDate = await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => tenantIds.Contains(m.TenantId))
                .MaxAsync(m => m.MetricDate);

            var rows = await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => tenantIds.Contains(m.TenantId) && m.MetricDate == latestDate)
                .ToListAsync();

            return rows.Select(ToDto).ToList();
        }

        public async Task UpsertMetricsAsync(string tenantId, DateTime metricDate, List<BrokerPerformanceMetrics> metrics)
        {
            var existing = await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => m.TenantId == tenantId && m.MetricDate == metricDate)
                .ToListAsync();

            _dbContext.Set<BrokerPerformanceMetrics>().RemoveRange(existing);

            await _dbContext.Set<BrokerPerformanceMetrics>().AddRangeAsync(metrics);
            await _dbContext.SaveChangesAsync();
        }

        private static BrokerPerformanceDto ToDto(BrokerPerformanceMetrics m)
        {
            return new BrokerPerformanceDto
            {
                BrokerId = m.BrokerId,
                LeadToDealConversionRate = m.LeadToDealConversionRate,
                AvgDaysToClose = m.AvgDaysToClose,
                AvgDealValue = m.AvgDealValue,
                AvgCommissionGenerated = m.AvgCommissionGenerated,
                StageVelocityScore = m.StageVelocityScore,
                GeoSpecializationScore = m.GeoSpecializationScore,
                PropertyTypeScore = ParseDictionary(m.PropertyTypeConversionScore),
                PriceBandScore = ParseDictionary(m.PriceBandScore),
                CrmHygieneScore = m.CrmHygieneScore,
                OverallScore = m.OverallScore,
                TotalLeadsAssigned = m.TotalLeadsAssigned,
                TotalDealsWon = m.TotalDealsWon
            };
        }

        private static Dictionary<string, decimal> ParseDictionary(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return new Dictionary<string, decimal>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? new();
            }
            catch
            {
                return new Dictionary<string, decimal>();
            }
        }
    }
}
