using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="BrochureCard"/> aggregate root.
/// </summary>
public class BrochureCardRepository : Repository<BrochureCard>, IBrochureCardRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BrochureCardRepository"/> class.
    /// </summary>
    public BrochureCardRepository(CRMDbContext dbContext, ILogger<Repository<BrochureCard>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BrochureCard>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(card => card.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
