using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="LeadWorkflow"/> aggregate root.
/// </summary>
public interface ILeadWorkflowRepository : IRepository<LeadWorkflow>
{
    /// <summary>
    /// Retrieves all workflows for a specific lead.
    /// </summary>
    Task<IReadOnlyList<LeadWorkflow>> GetByLeadIdAsync(int leadId, CancellationToken cancellationToken = default);
}
