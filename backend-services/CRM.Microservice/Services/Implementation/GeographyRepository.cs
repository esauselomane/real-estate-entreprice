using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="GeographyRecord"/> aggregate root.
/// </summary>
public class GeographyRepository : Repository<GeographyRecord>, IGeographyRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeographyRepository"/> class.
    /// </summary>
    public GeographyRepository(CRMDbContext dbContext, ILogger<Repository<GeographyRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GeographyRecord>> GetByCityAsync(string city, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(geo => geo.City == city)
            .ToListAsync(cancellationToken);
    }
}
