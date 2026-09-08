using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Capability for querying workflow entities by stage.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public interface IStageable<T> where T : class
{
    /// <summary>
    /// Retrieves entities by their workflow stage.
    /// </summary>
    Task<IReadOnlyList<T>> GetByStageAsync(string stage, CancellationToken cancellationToken = default);
}
