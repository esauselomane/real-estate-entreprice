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

    public UserGrowthRepository(CRMDbContext dbContext, ILogger<UserGrowthRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
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
            var agency = await _dbContext.AgencyRecords
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (agency != null)
            {
                if (agency.IsTrial && agency.TrialStartDate.HasValue)
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
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetSubscriptionHistoryAsync: could not load subscription data for tenant {TenantId}", tenantId);
        }

        return (active, past);
    }
}
