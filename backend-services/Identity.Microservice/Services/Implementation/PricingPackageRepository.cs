using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="PricingPackage"/> aggregate root.
/// </summary>
public class PricingPackageRepository : Repository<PricingPackage>, IPricingPackageRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PricingPackageRepository"/> class.
    /// </summary>
    public PricingPackageRepository(IdentityDbContext dbContext, ILogger<Repository<PricingPackage>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<PricingPackage?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(pkg => pkg.Code == code, cancellationToken);
    }
}
