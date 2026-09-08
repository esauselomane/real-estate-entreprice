using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="OwnerTenantRecord"/> aggregate root.
/// </summary>
public class OwnerTenantRepository : Repository<OwnerTenantRecord>, IOwnerTenantRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OwnerTenantRepository"/> class.
    /// </summary>
    public OwnerTenantRepository(CRMDbContext dbContext, ILogger<Repository<OwnerTenantRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OwnerTenantRecord>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(record => record.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
