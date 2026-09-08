using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="PropertyAsset"/> aggregate root.
/// </summary>
public class PropertyRepository : Repository<PropertyAsset>, IPropertyRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyRepository"/> class.
    /// </summary>
    public PropertyRepository(PropertyDbContext dbContext, ILogger<Repository<PropertyAsset>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PropertyAsset>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(property => property.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
