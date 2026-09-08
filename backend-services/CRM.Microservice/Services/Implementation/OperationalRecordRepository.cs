using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="OperationalRecord"/> aggregate root.
/// </summary>
public class OperationalRecordRepository : Repository<OperationalRecord>, IOperationalRecordRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OperationalRecordRepository"/> class.
    /// </summary>
    public OperationalRecordRepository(CRMDbContext dbContext, ILogger<Repository<OperationalRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OperationalRecord>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(record => record.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
