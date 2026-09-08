using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="DealRecord"/> aggregate root.
/// </summary>
public class DealRepository : Repository<DealRecord>, IDealRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DealRepository"/> class.
    /// </summary>
    public DealRepository(CRMDbContext dbContext, ILogger<Repository<DealRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DealRecord>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(deal => deal.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DealRecord>> GetByStageAsync(string stage, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(deal => deal.DealStage.ToString() == stage)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DealRecord>> GetByBrokerAsync(string brokerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(deal => deal.AllocatedBrokerIds.Contains(brokerId))
            .ToListAsync(cancellationToken);
    }
}
