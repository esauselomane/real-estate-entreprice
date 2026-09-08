using Property.Microservice.DTOs;
using Property.Microservice.Models;

namespace Property.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="PropertyUnit"/> aggregate root.
/// </summary>
public interface IPropertyUnitRepository : IRepository<PropertyUnit>
{
    /// <summary>
    /// Retrieves all property units for a specific property asset.
    /// </summary>
    Task<IReadOnlyList<PropertyUnit>> GetByPropertyAssetIdAsync(int propertyAssetId, CancellationToken cancellationToken = default);
}
