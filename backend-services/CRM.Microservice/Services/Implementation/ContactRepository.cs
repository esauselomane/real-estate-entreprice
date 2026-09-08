using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="CRMContact"/> aggregate root.
/// </summary>
public class ContactRepository : Repository<CRMContact>, IContactRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactRepository"/> class.
    /// </summary>
    public ContactRepository(CRMDbContext dbContext, ILogger<Repository<CRMContact>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CRMContact>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(contact => contact.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CRMContact?> GetByEmailAsync(string tenantId, string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(contact => contact.TenantId == tenantId && contact.EmailAddress == email, cancellationToken);
    }
}
