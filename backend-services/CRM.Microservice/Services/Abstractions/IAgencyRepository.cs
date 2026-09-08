using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="AgencyRecord"/> aggregate root.
/// </summary>
public interface IAgencyRepository : IRepository<AgencyRecord>, ITenantScoped<AgencyRecord>
{
    /// <summary>
    /// Retrieves an agency with all related child entities.
    /// </summary>
    Task<AgencyRecord?> GetWithChildrenAsync(string agencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full agency subtree for multi-tenant hierarchy traversal.
    /// </summary>
    Task<IReadOnlyList<AgencyRecord>> GetSubtreeAsync(string parentAgencyId, CancellationToken cancellationToken = default);
}
