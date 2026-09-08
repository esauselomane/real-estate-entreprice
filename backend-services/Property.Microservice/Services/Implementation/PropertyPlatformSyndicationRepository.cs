using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="PropertyPlatformSyndication"/> aggregate root.
/// </summary>
public class PropertyPlatformSyndicationRepository : Repository<PropertyPlatformSyndication>, IRepository<PropertyPlatformSyndication>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyPlatformSyndicationRepository"/> class.
    /// </summary>
    public PropertyPlatformSyndicationRepository(PropertyDbContext dbContext, ILogger<Repository<PropertyPlatformSyndication>> logger) : base(dbContext, logger)
    {
    }
}
