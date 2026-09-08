using CRM.Microservice.DTOs;
using CRM.Microservice.Models;

namespace CRM.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="GeographyRecord"/> aggregate root.
/// </summary>
public interface IGeographyRepository : IRepository<GeographyRecord>
{
    /// <summary>
    /// Retrieves all geography records for a specific city.
    /// </summary>
    Task<IReadOnlyList<GeographyRecord>> GetByCityAsync(string city, CancellationToken cancellationToken = default);
}
