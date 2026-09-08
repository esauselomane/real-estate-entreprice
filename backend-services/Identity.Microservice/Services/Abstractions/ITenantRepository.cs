using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="TenantAgency"/> aggregate root.
/// </summary>
public interface ITenantRepository : IRepository<TenantAgency>
{
    /// <summary>
    /// Retrieves a tenant agency by its tenant identifier.
    /// </summary>
    Task<TenantAgency?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
}
