using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="ExternalPlatform"/> aggregate root.
/// </summary>
public class ExternalPlatformRepository : Repository<ExternalPlatform>, IRepository<ExternalPlatform>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalPlatformRepository"/> class.
    /// </summary>
    public ExternalPlatformRepository(PropertyDbContext dbContext, ILogger<Repository<ExternalPlatform>> logger) : base(dbContext, logger)
    {
    }
}
