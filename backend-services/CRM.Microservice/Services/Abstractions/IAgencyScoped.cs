using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Capability for querying entities by agency identifier.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IAgencyScoped<T> where T : class
{
    /// <summary>
    /// Retrieves all entities for a specific agency.
    /// </summary>
    Task<IReadOnlyList<T>> GetByAgencyIdAsync(string agencyId, CancellationToken cancellationToken = default);
}
