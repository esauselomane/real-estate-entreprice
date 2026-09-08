using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="AmenityType"/> aggregate root.
/// </summary>
public class AmenityTypeRepository : Repository<AmenityType>, IRepository<AmenityType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmenityTypeRepository"/> class.
    /// </summary>
    public AmenityTypeRepository(PropertyDbContext dbContext, ILogger<Repository<AmenityType>> logger) : base(dbContext, logger)
    {
    }
}
