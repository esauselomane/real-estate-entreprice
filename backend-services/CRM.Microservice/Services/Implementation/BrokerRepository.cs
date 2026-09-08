using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="BrokerRecord"/> aggregate root.
/// </summary>
public class BrokerRepository : Repository<BrokerRecord>, IBrokerRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerRepository"/> class.
    /// </summary>
    public BrokerRepository(CRMDbContext dbContext, ILogger<Repository<BrokerRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BrokerRecord>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(broker => broker.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BrokerRecord>> GetByAgencyIdAsync(string agencyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(broker => broker.AgencyId == agencyId)
            .ToListAsync(cancellationToken);
    }
}
