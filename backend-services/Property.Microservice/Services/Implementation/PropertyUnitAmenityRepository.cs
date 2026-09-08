using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="PropertyUnitAmenity"/> aggregate root.
/// </summary>
public class PropertyUnitAmenityRepository : Repository<PropertyUnitAmenity>, IRepository<PropertyUnitAmenity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyUnitAmenityRepository"/> class.
    /// </summary>
    public PropertyUnitAmenityRepository(PropertyDbContext dbContext, ILogger<Repository<PropertyUnitAmenity>> logger) : base(dbContext, logger)
    {
    }
}
