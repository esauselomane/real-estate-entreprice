using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="BrochureCardLine"/> aggregate root.
/// </summary>
public class BrochureCardLineRepository : Repository<BrochureCardLine>, IRepository<BrochureCardLine>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BrochureCardLineRepository"/> class.
    /// </summary>
    public BrochureCardLineRepository(CRMDbContext dbContext, ILogger<Repository<BrochureCardLine>> logger) : base(dbContext, logger)
    {
    }
}
