using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="CRMLegalEntity"/> aggregate root.
/// </summary>
public class LegalEntityRepository : Repository<CRMLegalEntity>, ILegalEntityRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LegalEntityRepository"/> class.
    /// </summary>
    public LegalEntityRepository(CRMDbContext dbContext, ILogger<Repository<CRMLegalEntity>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CRMLegalEntity>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(entity => entity.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
