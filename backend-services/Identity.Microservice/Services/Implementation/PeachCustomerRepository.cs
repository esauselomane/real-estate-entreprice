using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation
{
    /// <summary>
    /// Repository implementation for <see cref="PeachCustomer"/> aggregate root.
    /// </summary>
    public class PeachCustomerRepository : Repository<PeachCustomer>, IPeachCustomerRepository
    {
        public PeachCustomerRepository(IdentityDbContext dbContext, ILogger<Repository<PeachCustomer>> logger) 
            : base(dbContext, logger)
        {
        }

        public async Task<PeachCustomer?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(pc => pc.TenantId == tenantId, cancellationToken);
        }

        public async Task<PeachCustomer?> GetByPeachCustomerIdAsync(string peachCustomerId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(pc => pc.PeachCustomerId == peachCustomerId, cancellationToken);
        }
    }
}