using System.Globalization;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for user/tenant growth analytics.
/// Provides user counts over time and subscription history for the Agency Glance dashboard.
/// </summary>
public class UserGrowthRepository : IUserGrowthRepository
{
    private readonly CRMDbContext _dbContext;
    private readonly ILogger<UserGrowthRepository> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _identityBaseUrl;

    public UserGrowthRepository(
        CRMDbContext dbContext,
        ILogger<UserGrowthRepository> logger,
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _identityBaseUrl = configuration["Identity:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("IDENTITY_BASE_URL")
            ?? "http://identity:5001";
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserGrowthPoint>> GetUsersGrowthOver6MonthsAsync(IReadOnlyList<string> tenantIds)
    {
        if (tenantIds == null || tenantIds.Count == 0)
            return Array.Empty<UserGrowthPoint>();

        var now = DateTime.UtcNow;
        var months = new List<DateTime>();
        for (int i = 5; i >= 0; i--)
        {
            months.Add(new DateTime(now.Year, now.Month, 1).AddMonths(-i));
        }

        var points = new List<UserGrowthPoint>();
        foreach (var monthStart in months)
        {
            var monthEnd = monthStart.AddMonths(1);
            var monthLabel = monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            var count = await _dbContext.BrokerRecords
                .Where(b => tenantIds.Contains(b.TenantId)
                    && b.CreatedAt >= monthStart && b.CreatedAt < monthEnd)
                .CountAsync();

            points.Add(new UserGrowthPoint
            {
                Month = monthLabel,
                UserCount = count
            });
        }

        return points;
    }

    /// <inheritdoc/>
    public async Task<(List<SubscriptionPackageDto> active, List<SubscriptionPackageDto> past)> GetSubscriptionHistoryAsync(string tenantId)
    {
        var active = new List<SubscriptionPackageDto>();
        var past = new List<SubscriptionPackageDto>();

        try
        {
            var client = _httpClientFactory.CreateClient();
            var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", string.Empty));
            }

            var response = await client.GetAsync($"{_identityBaseUrl}/api/payments/subscription/history?tenantId={Uri.EscapeDataString(tenantId)}");
            if (!response.IsSuccessStatusCode)
            {
                return await GetTrialFallbackAsync(tenantId, active, past);
            }

            var payload = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload) || payload == "[]")
            {
                return await GetTrialFallbackAsync(tenantId, active, past);
            }

            var statusItems = System.Text.Json.JsonSerializer.Deserialize<List<IdentitySubscriptionStatusResponse>>(payload);
            if (statusItems == null || statusItems.Count == 0)
            {
                return await GetTrialFallbackAsync(tenantId, active, past);
            }

            foreach (var item in statusItems)
            {
                var package = new SubscriptionPackageDto
                {
                    Id = 0,
                    Code = item.PricingPackageCode ?? "UNKNOWN",
                    Name = item.PricingPackageName ?? item.PricingPackageCode ?? "Subscription",
                    MonthlyPrice = item.MonthlyPrice,
                    AnnualPrice = item.AnnualPrice,
                    StartDate = item.CurrentPeriodStart,
                    EndDate = item.CurrentPeriodEnd,
                    Status = item.Status ?? "none"
                };

                if (string.Equals(item.Status, "active", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.Status, "trialing", StringComparison.OrdinalIgnoreCase))
                {
                    active.Add(package);
                }
                else if (!string.IsNullOrWhiteSpace(item.PricingPackageCode) && !string.Equals(item.Status, "none", StringComparison.OrdinalIgnoreCase))
                {
                    past.Add(package);
                }
            }

            return (active, past);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetSubscriptionHistoryAsync: could not load subscription data for tenant {TenantId}", tenantId);
        }

        return await GetTrialFallbackAsync(tenantId, active, past);
    }

    private async Task<(List<SubscriptionPackageDto> active, List<SubscriptionPackageDto> past)> GetTrialFallbackAsync(string tenantId, List<SubscriptionPackageDto> active, List<SubscriptionPackageDto> past)
    {
        var agency = await _dbContext.AgencyRecords
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (agency != null && agency.IsTrial && agency.TrialStartDate.HasValue)
        {
            var endDate = agency.TrialEndDate ?? agency.TrialStartDate.Value.AddDays(14);
            var status = DateTime.UtcNow < endDate ? "Active" : "Expired";
            var pkg = new SubscriptionPackageDto
            {
                Id = 0,
                Code = "TRIAL",
                Name = "Free Trial",
                MonthlyPrice = null,
                AnnualPrice = null,
                StartDate = agency.TrialStartDate,
                EndDate = endDate,
                Status = status
            };

            if (status == "Active")
                active.Add(pkg);
            else
                past.Add(pkg);
        }

        return (active, past);
    }

    private sealed class IdentitySubscriptionStatusResponse
    {
        public string? Status { get; set; }
        public string? PricingPackageCode { get; set; }
        public string? PricingPackageName { get; set; }
        public decimal? MonthlyPrice { get; set; }
        public decimal? AnnualPrice { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
    }
}
