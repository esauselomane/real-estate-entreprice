using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions
{
    /// <summary>
    /// Repository contract for <see cref="Subscription"/> aggregate root.
    /// </summary>
    public interface ISubscriptionRepository : IRepository<Subscription>
    {
        Task<Subscription?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<Subscription?> GetByPeachSubscriptionIdAsync(string peachSubscriptionId, CancellationToken cancellationToken = default);
        Task<Subscription?> GetActiveByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    }
}