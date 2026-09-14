using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation
{
    /// <summary>
    /// Repository implementation for <see cref="Subscription"/> aggregate root.
    /// </summary>
    public class SubscriptionRepository : Repository<Subscription>, ISubscriptionRepository
    {
        public SubscriptionRepository(IdentityDbContext dbContext, ILogger<Repository<Subscription>> logger) 
            : base(dbContext, logger)
        {
        }

        public async Task<Subscription?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        }

        public async Task<Subscription?> GetByPeachSubscriptionIdAsync(string peachSubscriptionId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(s => s.PeachSubscriptionId == peachSubscriptionId, cancellationToken);
        }

        public async Task<Subscription?> GetActiveByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status != SubscriptionStatus.Cancelled && s.Status != SubscriptionStatus.Expired, cancellationToken);
        }
    }
}