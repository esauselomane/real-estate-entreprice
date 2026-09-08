using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="RolePermission"/> join entity.
/// </summary>
public class RolePermissionRepository : Repository<RolePermission>, IRolePermissionRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RolePermissionRepository"/> class.
    /// </summary>
    public RolePermissionRepository(IdentityDbContext dbContext, ILogger<Repository<RolePermission>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(int roleDefId, int permissionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(rp => rp.RoleDefId == roleDefId && rp.PermissionId == permissionId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RolePermission>> GetByRoleAsync(int roleDefId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rp => rp.RoleDefId == roleDefId)
            .ToListAsync(cancellationToken);
    }
}
