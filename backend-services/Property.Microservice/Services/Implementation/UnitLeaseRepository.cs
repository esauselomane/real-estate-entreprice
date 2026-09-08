using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="UnitLease"/> aggregate root.
/// </summary>
public class UnitLeaseRepository : Repository<UnitLease>, IRepository<UnitLease>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnitLeaseRepository"/> class.
    /// </summary>
    public UnitLeaseRepository(PropertyDbContext dbContext, ILogger<Repository<UnitLease>> logger) : base(dbContext, logger)
    {
    }
}
