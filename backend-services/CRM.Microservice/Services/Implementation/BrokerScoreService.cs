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

            _weights["PropertyType"] = 0m;
            var totalWeight = _weights.Values.Sum(weight => Math.Max(0m, weight));
            if (totalWeight <= 0m) throw new InvalidOperationException("Broker scoring requires at least one positive weight.");
            foreach (var key in _weights.Keys.ToList()) _weights[key] = Math.Max(0m, _weights[key]) / totalWeight;

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

            var allDeals = await _dbContext.DealRecords.AsNoTracking().Where(deal => deal.TenantId == tenantId).ToListAsync();
            var allLeads = await _dbContext.LeadRecords.AsNoTracking().Where(lead => lead.TenantId == tenantId).ToListAsync();
            var metricDate = DateTime.UtcNow.Date;

            var metrics = new List<BrokerPerformanceMetrics>();
            foreach (var broker in brokers)
            {
                var metric = await ComputeSingleBrokerMetricsAsync(broker, tenantId, metricDate, allDeals, allLeads);
                metrics.Add(metric);
            }

            return metrics;
        }

        private async Task<BrokerPerformanceMetrics> ComputeSingleBrokerMetricsAsync(
            BrokerRecord broker, string tenantId, DateTime metricDate, IReadOnlyList<DealRecord> allDeals, IReadOnlyList<LeadRecord> allLeads)
        {
            var brokerId = broker.BrokerId;

            var deals = allDeals.Where(deal => IsAssigned(deal.AllocatedBrokerIds, brokerId)).ToList();
            var leads = allLeads.Where(lead => IsAssigned(lead.AllocatedBrokerIds, brokerId)).ToList();

            var (conversionRate, avgDaysToClose, avgDealValue, avgCommission) = ComputeDealMetrics(deals, leads);
            var stageVelocityScore = await ComputeStageVelocityScoreAsync(deals);
            var (geoScore, geoDetails) = ComputeGeoSpecializationScore(deals, leads);
            var propertyTypeScore = new Dictionary<string, decimal>(); // Deal records do not store a reliable property type.
            var priceBandScore = ComputePriceBandScore(deals);
            var hygieneScore = await ComputeCrmHygieneScoreAsync(brokerId, tenantId, leads);

            var overallScore = ComputeOverallScore(
                conversionRate, avgDaysToClose, avgCommission, stageVelocityScore, geoScore,
                propertyTypeScore, priceBandScore, hygieneScore);

            if (leads.Count == 0 && deals.Count == 0) overallScore = 0m;
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

            var dealsWon = deals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).ToList();
            var conversionRate = totalLeads > 0 ? (decimal)leads.Count(lead => lead.IsConvertedToDeal) / totalLeads * 100 : 0;
            var avgDealValue = dealsWon.Any() ? dealsWon.Average(d => d.TransactionValue) : 0;
            var avgCommission = dealsWon.Count > 0 ? dealsWon.Average(Reporting.CommissionCalculator.Realised) : 0m;

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
                    (o.Title.Contains(brokerId) ||
                     o.Details.Contains(brokerId)));

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
                : 0;

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
            var metricsDtos = (await ComputeAllBrokerMetricsAsync(tenantId)).Select(ToPerformanceDto).ToList();

            if (!metricsDtos.Any())
                return new List<SuggestedBrokerDto>();

            var brokers = await _brokerRepository.Query()
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.Status == "Active")
                .ToDictionaryAsync(b => b.BrokerId, b => b);

            var results = new List<SuggestedBrokerDto>();

            foreach (var dto in metricsDtos)
            {
                if (!brokers.ContainsKey(dto.BrokerId)) continue;
                var adjustedScore = Math.Clamp(dto.OverallScore, 0m, 100m);

                var label = GetScoreLabel(adjustedScore);
                var brokerName = brokers.TryGetValue(dto.BrokerId, out var broker) ? broker.FullName : dto.BrokerId;

                results.Add(new SuggestedBrokerDto
                {
                    BrokerId = dto.BrokerId,
                    BrokerName = brokerName,
                    MatchScore = Math.Round(adjustedScore, 2),
                    Label = dto.TotalLeadsAssigned == 0 && dto.TotalDealsWon == 0 ? "Insufficient history" : label,
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
                .ThenBy(r => r.BrokerId, StringComparer.Ordinal)
                .Take(3)
                .ToList();
        }

        private static bool IsAssigned(string allocation, string brokerId) => allocation
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(brokerId, StringComparer.OrdinalIgnoreCase);

        private static BrokerPerformanceDto ToPerformanceDto(BrokerPerformanceMetrics metric) => new()
        {
            BrokerId = metric.BrokerId, OverallScore = metric.OverallScore,
            LeadToDealConversionRate = metric.LeadToDealConversionRate, AvgDaysToClose = metric.AvgDaysToClose,
            AvgDealValue = metric.AvgDealValue, TotalLeadsAssigned = metric.TotalLeadsAssigned, TotalDealsWon = metric.TotalDealsWon
        };

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
