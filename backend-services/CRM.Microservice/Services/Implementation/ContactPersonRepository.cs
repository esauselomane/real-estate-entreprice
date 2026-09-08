using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="ContactPerson"/> aggregate root.
/// </summary>
public class ContactPersonRepository : Repository<ContactPerson>, IContactPersonRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactPersonRepository"/> class.
    /// </summary>
    public ContactPersonRepository(CRMDbContext dbContext, ILogger<Repository<ContactPerson>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContactPerson>> GetByAgencyIdAsync(string agencyId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(contactPerson => contactPerson.AgencyId == agencyId)
            .ToListAsync(cancellationToken);
    }
}
