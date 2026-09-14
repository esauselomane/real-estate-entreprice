using CRM.Microservice.DTOs;
using Microsoft.AspNetCore.Http;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Service contract for querying the Property microservice to obtain
/// property-level counts that the CRM database does not store.
/// </summary>
public interface IPropertyCountService
{
    /// <summary>
    /// Returns the total number of properties with active sole mandates across
    /// the provided tenant IDs. Calls the Property microservice over HTTP.
    /// </summary>
    Task<int?> GetTotalSoleMandatesAsync(IReadOnlyList<string> tenantIds, HttpContext httpContext);

    /// <summary>
    /// Returns cumulative month-end property totals for the last 6 months (current month to date), scoped to
    /// the provided tenant IDs. Calls the Property microservice over HTTP.
    /// </summary>
    Task<IReadOnlyList<PropertyGrowthPoint>> GetPropertiesGrowthOver6MonthsAsync(IReadOnlyList<string> tenantIds, HttpContext httpContext);
}
