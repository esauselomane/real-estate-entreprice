using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="TenantAgency"/> aggregate root.
/// </summary>
public class TenantRepository : Repository<TenantAgency>, ITenantRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantRepository"/> class.
    /// </summary>
    public TenantRepository(IdentityDbContext dbContext, ILogger<Repository<TenantAgency>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<TenantAgency?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(tenant => tenant.TenantId == tenantId, cancellationToken);
    }
}
