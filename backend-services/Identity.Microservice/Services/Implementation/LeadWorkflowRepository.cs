using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="LeadWorkflow"/> aggregate root.
/// </summary>
public class LeadWorkflowRepository : Repository<LeadWorkflow>, ILeadWorkflowRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeadWorkflowRepository"/> class.
    /// </summary>
    public LeadWorkflowRepository(IdentityDbContext dbContext, ILogger<Repository<LeadWorkflow>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeadWorkflow>> GetByLeadIdAsync(int leadId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(workflow => workflow.LeadId == leadId)
            .ToListAsync(cancellationToken);
    }
}
