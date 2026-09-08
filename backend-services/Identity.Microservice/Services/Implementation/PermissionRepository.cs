using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="Permission"/> aggregate root.
/// </summary>
public class PermissionRepository : Repository<Permission>, IPermissionRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRepository"/> class.
    /// </summary>
    public PermissionRepository(IdentityDbContext dbContext, ILogger<Repository<Permission>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(permission => permission.Key == key, cancellationToken);
    }
}
