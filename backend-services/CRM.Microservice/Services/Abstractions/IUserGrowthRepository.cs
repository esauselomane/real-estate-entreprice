using CRM.Microservice.DTOs;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for user/tenant growth analytics.
/// Provides user counts over time and subscription history for the Agency Glance dashboard.
/// </summary>
public interface IUserGrowthRepository
{
    /// <summary>
    /// Returns the count of users (brokers) added per month for the last 6 months,
    /// scoped to the provided tenant IDs. Returns 6 data points, oldest first.
    /// </summary>
    Task<IReadOnlyList<UserGrowthPoint>> GetUsersGrowthOver6MonthsAsync(IReadOnlyList<string> tenantIds);

    /// <summary>
    /// Returns subscription packages for the given tenant: active ones in the first list,
    /// past/expired ones in the second list.
    /// </summary>
    Task<(List<SubscriptionPackageDto> active, List<SubscriptionPackageDto> past)> GetSubscriptionHistoryAsync(string tenantId);
}
