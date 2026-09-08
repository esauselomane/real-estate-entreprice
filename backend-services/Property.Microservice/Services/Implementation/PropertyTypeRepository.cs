using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="PropertyType"/> aggregate root.
/// </summary>
public class PropertyTypeRepository : Repository<PropertyType>, IRepository<PropertyType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyTypeRepository"/> class.
    /// </summary>
    public PropertyTypeRepository(PropertyDbContext dbContext, ILogger<Repository<PropertyType>> logger) : base(dbContext, logger)
    {
    }
}
