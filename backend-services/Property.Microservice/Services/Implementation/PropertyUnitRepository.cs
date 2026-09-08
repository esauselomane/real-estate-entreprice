using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="PropertyUnit"/> aggregate root.
/// </summary>
public class PropertyUnitRepository : Repository<PropertyUnit>, IPropertyUnitRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyUnitRepository"/> class.
    /// </summary>
    public PropertyUnitRepository(PropertyDbContext dbContext, ILogger<Repository<PropertyUnit>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PropertyUnit>> GetByPropertyAssetIdAsync(int propertyAssetId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(unit => unit.PropertyId == propertyAssetId)
            .ToListAsync(cancellationToken);
    }
}
