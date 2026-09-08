using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="DealBrokerSplit"/> aggregate root.
/// </summary>
public class DealBrokerSplitRepository : Repository<DealBrokerSplit>, IDealBrokerSplitRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DealBrokerSplitRepository"/> class.
    /// </summary>
    public DealBrokerSplitRepository(CRMDbContext dbContext, ILogger<Repository<DealBrokerSplit>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DealBrokerSplit>> GetByDealIdAsync(int dealId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(split => split.DealId == dealId)
            .ToListAsync(cancellationToken);
    }
}
