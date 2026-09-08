using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Data;
using Property.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Property.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="SoleMandate"/> aggregate root.
/// </summary>
public class SoleMandateRepository : Repository<SoleMandate>, IRepository<SoleMandate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoleMandateRepository"/> class.
    /// </summary>
    public SoleMandateRepository(PropertyDbContext dbContext, ILogger<Repository<SoleMandate>> logger) : base(dbContext, logger)
    {
    }
}
