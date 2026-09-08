using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Capability for looking up entities by email address.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IEmailLookup<T> where T : class
{
    /// <summary>
    /// Retrieves an entity by email address.
    /// </summary>
    Task<T?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
