using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation
{
    /// <summary>
    /// Repository implementation for <see cref="PaymentIntent"/> aggregate root.
    /// </summary>
    public class PaymentIntentRepository : Repository<PaymentIntent>, IPaymentIntentRepository
    {
        public PaymentIntentRepository(IdentityDbContext dbContext, ILogger<Repository<PaymentIntent>> logger) 
            : base(dbContext, logger)
        {
        }

        public async Task<PaymentIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(pi => pi.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        public async Task<PaymentIntent?> GetByPeachPaymentIdAsync(string peachPaymentId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(pi => pi.PeachPaymentId == peachPaymentId, cancellationToken);
        }

        public async Task<PaymentIntent?> GetByPeachCheckoutIdAsync(string peachCheckoutId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(pi => pi.PeachCheckoutId == peachCheckoutId, cancellationToken);
        }

        public async Task<List<PaymentIntent>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .Where(pi => pi.TenantId == tenantId)
                .OrderByDescending(pi => pi.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}