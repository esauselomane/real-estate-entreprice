using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;
using CRM.Microservice.DTOs;

namespace CRM.Microservice.Services.Implementation
{
    /// <summary>
    /// Computes broker performance metrics including conversion rates, deal velocity,
    /// geographic and property-type specialization, and CRM hygiene scores.
    /// </summary>
    public class BrokerScoreService : IBrokerScoreService
    {
        private readonly CRMDbContext _dbContext;
        private readonly IBrokerRepository _brokerRepository;
        private readonly ILogger<BrokerScoreService> _logger;
        private readonly IConfiguration _configuration;

        private readonly Dictionary<string, decimal> _weights;
        private readonly List<PriceBand> _priceBands;

        public BrokerScoreService(
            CRMDbContext dbContext,
            IBrokerRepository brokerRepository,
            IConfiguration configuration,
            ILogger<BrokerScoreService> logger)
        {
            _dbContext = dbContext;
            _brokerRepository = brokerRepository;
            _logger = logger;
            _configuration = configuration;

            _weights = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["ConversionRate"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:ConversionRate", 0.30m),
                ["DaysToClose"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:DaysToClose", 0.15m),
                ["CommissionGenerated"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:CommissionGenerated", 0.15m),
                ["StageVelocity"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:StageVelocity", 0.10m),
                ["GeoSpecialization"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:GeoSpecialization", 0.10m),
                ["PropertyType"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:PropertyType", 0.10m),
                ["PriceBand"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:PriceBand", 0.05m),
                ["CrmHygiene"] = _configuration.GetValue<decimal>("BrokerScoring:Weights:CrmHygiene", 0.05m)
            };

            _priceBands = new List<PriceBand>
            {
                new("R0-R500K",        0,       500_000),
                new("R500K-R1M",       500_000, 1_000_000),
                new("R1M-R2M",         1_000_000, 2_000_000),
                new("R2M-R5M",         2_000_000, 5_000_000),
                new("R5M+",            5_000_000, decimal.MaxValue)
            };
        }

        public async Task<List<BrokerPerformanceMetrics>> ComputeAllBrokerMetricsAsync(string tenantId)
        {
            var brokers = await _brokerRepository.Query()
                .Where(b => b.TenantId == tenantId)
                .ToListAsync();

            if (!brokers.Any())
                return new List<BrokerPerformanceMetrics>();

            var metricDate = DateTime.UtcNow.Date;

            var metrics = new List<BrokerPerformanceMetrics>();
            foreach (var broker in brokers)
            {
                var metric = await ComputeSingleBrokerMetricsAsync(broker, tenantId, metricDate);
                metrics.Add(metric);
            }

            return metrics;
        }

        private async Task<BrokerPerformanceMetrics> ComputeSingleBrokerMetricsAsync(
            BrokerRecord broker, string tenantId, DateTime metricDate)
        {
            var brokerId = broker.BrokerId;

            var deals = await _dbContext.DealRecords
                .AsNoTracking()
                .Where(d => d.TenantId == tenantId)
                .Where(d => d.AllocatedBrokerIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Any(b => string.Equals(b.Trim(), brokerId, StringComparison.OrdinalIgnoreCase)))
                .ToListAsync();

            var leads = await _dbContext.LeadRecords
                .AsNoTracking()
                .Where(l => l.TenantId == tenantId)
                .Where(l => l.AllocatedBrokerIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Any(b => string.Equals(b.Trim(), brokerId, StringComparison.OrdinalIgnoreCase)))
                .ToListAsync();

            var (conversionRate, avgDaysToClose, avgDealValue, avgCommission) = ComputeDealMetrics(deals, leads);
            var stageVelocityScore = await ComputeStageVelocityScoreAsync(deals);
            var (geoScore, geoDetails) = ComputeGeoSpecializationScore(deals, leads);
            var propertyTypeScore = ComputePropertyTypeScore(deals);
            var priceBandScore = ComputePriceBandScore(deals);
            var hygieneScore = await ComputeCrmHygieneScoreAsync(brokerId, tenantId, leads);

            var overallScore = ComputeOverallScore(
                conversionRate, avgDaysToClose, avgCommission, stageVelocityScore, geoScore,
                propertyTypeScore, priceBandScore, hygieneScore);

            var totalLeads = leads.Count;
            var dealsWon = deals.Count(d => d.DealStage == DealWorkflowStage.ClosedWon);

            return new BrokerPerformanceMetrics
            {
                TenantId = tenantId,
                BrokerId = brokerId,
                MetricDate = metricDate,
                LeadToDealConversionRate = Math.Round(conversionRate, 2),
                AvgDaysToClose = Math.Round(avgDaysToClose, 1),
                AvgDealValue = Math.Round(avgDealValue, 2),
                AvgCommissionGenerated = Math.Round(avgCommission, 2),
                StageVelocityScore = Math.Round(stageVelocityScore, 2),
                GeoSpecializationScore = Math.Round(geoScore, 2),
                PropertyTypeConversionScore = JsonSerializer.Serialize(propertyTypeScore),
                PriceBandScore = JsonSerializer.Serialize(priceBandScore),
                CrmHygieneScore = Math.Round(hygieneScore, 2),
                OverallScore = Math.Round(overallScore, 2),
                TotalLeadsAssigned = totalLeads,
                TotalDealsWon = dealsWon,
                ComputedAt = DateTime.UtcNow
            };
        }

        private static (decimal conversionRate, decimal avgDaysToClose, decimal avgDealValue, decimal avgCommission)
            ComputeDealMetrics(List<DealRecord> deals, List<LeadRecord> leads)
        {
            var totalLeads = leads.Count;
            if (totalLeads == 0)
                return (0, 0, 0, 0);

            var dealsWon = deals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).ToList();
            var conversionRate = totalLeads > 0 ? (decimal)dealsWon.Count / totalLeads * 100 : 0;
            var avgDealValue = dealsWon.Any() ? dealsWon.Average(d => d.TransactionValue) : 0;
            var avgCommission = dealsWon.Any() && dealsWon.Any(d => d.RealisedCommission.HasValue)
                ? dealsWon.Where(d => d.RealisedCommission.HasValue).Average(d => d.RealisedCommission!.Value)
                : 0;

            var closedDealsWithDates = dealsWon
                .Where(d => d.LastStageUpdatedAt > d.CreatedAt)
                .Select(d => (d.LastStageUpdatedAt - d.CreatedAt).TotalDays)
                .Where(days => days > 0)
                .ToList();

            var avgDaysToClose = closedDealsWithDates.Any()
                ? (decimal)closedDealsWithDates.Average()
                : 0;

            return (conversionRate, avgDaysToClose, avgDealValue, avgCommission);
        }

        private async Task<decimal> ComputeStageVelocityScoreAsync(List<DealRecord> deals)
        {
            if (!deals.Any())
                return 0;

            var stageCounts = new[]
            {
                CountDealsInStage(deals, DealWorkflowStage.Presentation),
                CountDealsInStage(deals, DealWorkflowStage.ProposalQuoteSent),
                CountDealsInStage(deals, DealWorkflowStage.Negotiation)
            };

            var totalActive = stageCounts.Sum();
            if (totalActive == 0)
                return 100;

            var progressed = stageCounts.Skip(1).Sum();
            var velocityScore = (decimal)progressed / totalActive * 100;

            return Math.Min(100, velocityScore);
        }

        private static int CountDealsInStage(List<DealRecord> deals, DealWorkflowStage stage)
        {
            return deals.Count(d => d.DealStage == stage);
        }

        private static (decimal score, Dictionary<string, decimal> geoDetails) ComputeGeoSpecializationScore(
            List<DealRecord> deals, List<LeadRecord> leads)
        {
            if (!leads.Any())
                return (0, new Dictionary<string, decimal>());

            var areaLeads = leads
                .Where(l => !string.IsNullOrWhiteSpace(l.PreferredArea))
                .GroupBy(l => l.PreferredArea)
                .ToDictionary(g => g.Key, g => g.Count());

            var areaConversion = new Dictionary<string, decimal>();
            var geoDetails = new Dictionary<string, decimal>();

            var dealsWon = deals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).ToList();

            foreach (var area in areaLeads.Keys)
            {
                var areaLeadCount = areaLeads[area];
                var areaDealCount = dealsWon.Count(d =>
                    !string.IsNullOrEmpty(d.PropertyAddress) &&
                    d.PropertyAddress.Contains(area, StringComparison.OrdinalIgnoreCase));

                var convRate = areaLeadCount > 0 ? (decimal)areaDealCount / areaLeadCount * 100 : 0;
                areaConversion[area] = convRate;
                geoDetails[area] = convRate;
            }

            var maxConversion = areaConversion.Values.DefaultIfEmpty(0).Max();
            var avgConversion = areaConversion.Values.Any()
                ? areaConversion.Values.Average()
                : 0;

            var normalizedScore = maxConversion > 0
                ? (avgConversion / Math.Max(1, maxConversion)) * 100
                : 0;

            return (Math.Min(100, normalizedScore), geoDetails);
        }

        private static Dictionary<string, decimal> ComputePropertyTypeScore(List<DealRecord> deals)
        {
            var dealsWon = deals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).ToList();
            if (!dealsWon.Any())
                return new Dictionary<string, decimal>();

            var byType = dealsWon
                .GroupBy(d => d.PropertyAddress ?? "Unknown")
                .ToDictionary(g => g.Key, g => (decimal)g.Count());

            var total = byType.Values.Sum();
            return byType.ToDictionary(kvp => kvp.Key, kvp => Math.Round(kvp.Value / total * 100, 2));
        }

        private Dictionary<string, decimal> ComputePriceBandScore(List<DealRecord> deals)
        {
            var result = new Dictionary<string, decimal>();
            var dealsWon = deals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).ToList();

            foreach (var band in _priceBands)
            {
                var inBand = dealsWon.Count(d => d.TransactionValue >= band.Min && d.TransactionValue < band.Max);
                result[band.Label] = dealsWon.Any() ? Math.Round((decimal)inBand / dealsWon.Count * 100, 2) : 0;
            }

            return result;
        }

        private async Task<decimal> ComputeCrmHygieneScoreAsync(string brokerId, string tenantId, List<LeadRecord> leads)
        {
            var score = 100m;

            if (leads.Any())
            {
                var leadsWithoutNotes = leads.Count(l => string.IsNullOrWhiteSpace(l.LeadNotes));
                var notesPenalty = (decimal)leadsWithoutNotes / leads.Count * 100;
                score -= Math.Min(50, notesPenalty * 0.5m);
            }

            var activityCount = await _dbContext.OperationalRecords
                .AsNoTracking()
                .CountAsync(o => o.TenantId == tenantId &&
                    (o.Title.Contains(brokerId, StringComparison.OrdinalIgnoreCase) ||
                     o.Details.Contains(brokerId, StringComparison.OrdinalIgnoreCase)));

            if (activityCount < 5)
                score -= 20;

            return Math.Max(0, score);
        }

        private decimal ComputeOverallScore(
            decimal conversionRate,
            decimal avgDaysToClose,
            decimal avgCommission,
            decimal stageVelocityScore,
            decimal geoScore,
            Dictionary<string, decimal> propertyTypeScore,
            Dictionary<string, decimal> priceBandScore,
            decimal hygieneScore)
        {
            var daysToCloseScore = avgDaysToClose > 0
                ? Math.Max(0, 100 - (avgDaysToClose / 30) * 100)
                : 100;

            var commissionScore = 0m;
            if (avgCommission > 0)
            {
                commissionScore = avgCommission < 10_000 ? avgCommission / 10000 * 100
                    : avgCommission < 100_000 ? 100
                    : 100;
            }

            var propertyTypeMax = propertyTypeScore.Values.DefaultIfEmpty(0).Max();
            var priceBandMax = priceBandScore.Values.DefaultIfEmpty(0).Max();

            return (conversionRate * _weights["ConversionRate"]) +
                   (daysToCloseScore * _weights["DaysToClose"]) +
                   (commissionScore * _weights["CommissionGenerated"]) +
                   (stageVelocityScore * _weights["StageVelocity"]) +
                   (geoScore * _weights["GeoSpecialization"]) +
                   (propertyTypeMax * _weights["PropertyType"]) +
                   (priceBandMax * _weights["PriceBand"]) +
                   (hygieneScore * _weights["CrmHygiene"]);
        }

        public async Task<List<SuggestedBrokerDto>> GetSuggestedBrokersAsync(
            string tenantId,
            decimal? budgetMin,
            decimal? budgetMax,
            string? propertyType,
            string? area,
            string currentUserId = "")
        {
            var metricsDtos = await GetLatestMetricsFromRepoAsync(tenantId);

            if (!metricsDtos.Any())
                return new List<SuggestedBrokerDto>();

            var brokers = await _brokerRepository.Query()
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId)
                .ToDictionaryAsync(b => b.BrokerId, b => b);

            var results = new List<SuggestedBrokerDto>();

            foreach (var dto in metricsDtos)
            {
                if (string.Equals(dto.BrokerId, currentUserId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var geoMatch = 1.0m;
                if (!string.IsNullOrWhiteSpace(area) && dto.GeoSpecializationScore > 0)
                {
                    var areaScore = dto.GeoSpecializationScore;
                    geoMatch = areaScore > 0 ? areaScore / 100 : 0.5m;
                }

                var priceBandScore = 0m;
                var priceMatch = 1.0m;
                if (budgetMin.HasValue && dto.PriceBandScore.Any())
                {
                    foreach (var band in _priceBands)
                    {
                        if (budgetMin >= band.Min && budgetMin < band.Max)
                        {
                            priceBandScore = dto.PriceBandScore.GetValueOrDefault(band.Label, 0);
                            priceMatch = priceBandScore > 0 ? priceBandScore / 100 : 0.5m;
                            break;
                        }
                    }
                }

                var propertyMatch = 1.0m;
                if (!string.IsNullOrWhiteSpace(propertyType) && dto.PropertyTypeScore.Any())
                {
                    propertyMatch = dto.PropertyTypeScore.GetValueOrDefault(propertyType, 0) > 0 ? 0.8m : 0.5m;
                }

                var rawScore = dto.OverallScore;
                var adjustedScore = rawScore * geoMatch * priceMatch * propertyMatch;

                var label = GetScoreLabel(adjustedScore);
                var brokerName = brokers.TryGetValue(dto.BrokerId, out var broker) ? broker.FullName : dto.BrokerId;

                results.Add(new SuggestedBrokerDto
                {
                    BrokerId = dto.BrokerId,
                    BrokerName = brokerName,
                    MatchScore = Math.Round(adjustedScore, 2),
                    Label = label,
                    ConversionRate = dto.LeadToDealConversionRate,
                    AvgDaysToClose = dto.AvgDaysToClose,
                    AvgDealValue = dto.AvgDealValue,
                    GeoSpecialization = dto.GeoSpecializationScore > 0
                        ? new Dictionary<string, decimal> { ["Overall"] = dto.GeoSpecializationScore }
                        : new Dictionary<string, decimal>()
                });
            }

            return results
                .OrderByDescending(r => r.MatchScore)
                .Take(3)
                .ToList();
        }

        private async Task<List<BrokerPerformanceDto>> GetLatestMetricsFromRepoAsync(string tenantId)
        {
            var latestDate = await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => m.TenantId == tenantId)
                .MaxAsync(m => (DateTime?)m.MetricDate) ?? DateTime.MinValue;

            return await _dbContext.Set<BrokerPerformanceMetrics>()
                .Where(m => m.TenantId == tenantId && m.MetricDate == latestDate)
                .Select(m => new BrokerPerformanceDto
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
                })
                .ToListAsync();
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

        private static string GetScoreLabel(decimal score)
        {
            if (score >= 85) return "Top Match";
            if (score >= 60) return "Good Match";
            if (score >= 40) return "Fair Match";
            return "Low Match";
        }
    }

    internal record PriceBand(string Label, decimal Min, decimal Max);
}
