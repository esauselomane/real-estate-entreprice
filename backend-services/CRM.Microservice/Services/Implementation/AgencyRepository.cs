using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="AgencyRecord"/> aggregate root.
/// </summary>
public class AgencyRepository : Repository<AgencyRecord>, IAgencyRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgencyRepository"/> class.
    /// </summary>
    public AgencyRepository(CRMDbContext dbContext, ILogger<Repository<AgencyRecord>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgencyRecord>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(agency => agency.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AgencyRecord?> GetWithChildrenAsync(string agencyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(agency => agency.AgencyId == agencyId)
            .Include(agency => agency.ContactPersons)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgencyRecord>> GetSubtreeAsync(string parentAgencyId, CancellationToken cancellationToken = default)
    {
        var all = await _dbSet.ToListAsync(cancellationToken);
        var descendants = new List<AgencyRecord>();
        var queue = new Queue<string>();
        queue.Enqueue(parentAgencyId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var children = all.Where(a => a.ParentAgencyId == currentId).ToList();
            descendants.AddRange(children);
            foreach (var child in children)
            {
                queue.Enqueue(child.AgencyId);
            }
        }

        return descendants;
    }
}
