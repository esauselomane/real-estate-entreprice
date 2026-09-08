using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Capability for querying entities by tenant identifier.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface ITenantScoped<T> where T : class
{
    /// <summary>
    /// Retrieves all entities for a specific tenant.
    /// </summary>
    Task<IReadOnlyList<T>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
}
