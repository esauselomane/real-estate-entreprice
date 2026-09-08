using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="MaintenanceCall"/> aggregate root.
/// </summary>
public class MaintenanceCallRepository : Repository<MaintenanceCall>, IRepository<MaintenanceCall>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaintenanceCallRepository"/> class.
    /// </summary>
    public MaintenanceCallRepository(PropertyDbContext dbContext, ILogger<Repository<MaintenanceCall>> logger) : base(dbContext, logger)
    {
    }
}
