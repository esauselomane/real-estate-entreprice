using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Capability for querying entities assigned to a specific broker.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IBrokerScoped<T> where T : class
{
    /// <summary>
    /// Retrieves entities assigned to a specific broker.
    /// </summary>
    Task<IReadOnlyList<T>> GetByBrokerAsync(string brokerId, CancellationToken cancellationToken = default);
}
