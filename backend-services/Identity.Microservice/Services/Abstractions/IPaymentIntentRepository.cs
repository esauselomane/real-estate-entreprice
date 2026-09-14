using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions
{
    /// <summary>
    /// Repository contract for <see cref="PaymentIntent"/> aggregate root.
    /// </summary>
    public interface IPaymentIntentRepository : IRepository<PaymentIntent>
    {
        Task<PaymentIntent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
        Task<PaymentIntent?> GetByPeachPaymentIdAsync(string peachPaymentId, CancellationToken cancellationToken = default);
        Task<PaymentIntent?> GetByPeachCheckoutIdAsync(string peachCheckoutId, CancellationToken cancellationToken = default);
        Task<List<PaymentIntent>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
    }
}