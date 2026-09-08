using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="DealBrokerSplit"/> aggregate root.
/// </summary>
public interface IDealBrokerSplitRepository : IRepository<DealBrokerSplit>
{
    /// <summary>
    /// Retrieves all broker splits for a specific deal.
    /// </summary>
    Task<IReadOnlyList<DealBrokerSplit>> GetByDealIdAsync(int dealId, CancellationToken cancellationToken = default);
}
