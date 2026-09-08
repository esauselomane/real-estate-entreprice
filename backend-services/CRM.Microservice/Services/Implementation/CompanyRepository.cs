using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="CRMCompany"/> aggregate root.
/// </summary>
public class CompanyRepository : Repository<CRMCompany>, ICompanyRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompanyRepository"/> class.
    /// </summary>
    public CompanyRepository(CRMDbContext dbContext, ILogger<Repository<CRMCompany>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CRMCompany>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(company => company.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
