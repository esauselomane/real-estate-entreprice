using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="LeadCaptureRequest"/> aggregate root.
/// </summary>
public class LeadCaptureRequestRepository : Repository<LeadCaptureRequest>, IRepository<LeadCaptureRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LeadCaptureRequestRepository"/> class.
    /// </summary>
    public LeadCaptureRequestRepository(IdentityDbContext dbContext, ILogger<Repository<LeadCaptureRequest>> logger) : base(dbContext, logger)
    {
    }
}
