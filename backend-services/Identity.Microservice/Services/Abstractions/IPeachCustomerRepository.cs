using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions
{
    /// <summary>
    /// Repository contract for <see cref="PeachCustomer"/> aggregate root.
    /// </summary>
    public interface IPeachCustomerRepository : IRepository<PeachCustomer>
    {
        Task<PeachCustomer?> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default);
        Task<PeachCustomer?> GetByPeachCustomerIdAsync(string peachCustomerId, CancellationToken cancellationToken = default);
    }
}