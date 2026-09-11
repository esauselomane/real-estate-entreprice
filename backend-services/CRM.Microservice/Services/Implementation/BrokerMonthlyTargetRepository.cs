using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="BrokerMonthlyTarget"/> records.
/// Tracks per-broker, per-month commission targets with historical entries.
/// </summary>
public class BrokerMonthlyTargetRepository : Repository<BrokerMonthlyTarget>, IRepository<BrokerMonthlyTarget>
{
    public BrokerMonthlyTargetRepository(CRMDbContext dbContext, ILogger<Repository<BrokerMonthlyTarget>> logger) : base(dbContext, logger)
    {
    }
}
