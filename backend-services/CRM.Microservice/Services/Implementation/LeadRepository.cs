using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="LeadRecord"/> aggregate root.
/// </summary>
public class LeadRepository : Repository<LeadRecord>, ILeadRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeadRepository"/> class.
    /// </summary>
    public LeadRepository(CRMDbContext dbContext, ILogger<Repository<LeadRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeadRecord>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(lead => lead.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeadRecord>> GetByStageAsync(string stage, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(lead => lead.LeadStage.ToString() == stage)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LeadRecord>> GetByBrokerAsync(string brokerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(lead => lead.AllocatedBrokerIds.Contains(brokerId))
            .ToListAsync(cancellationToken);
    }
}
