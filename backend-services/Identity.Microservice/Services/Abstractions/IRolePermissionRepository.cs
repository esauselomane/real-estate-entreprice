using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="RolePermission"/> join entity.
/// </summary>
public interface IRolePermissionRepository : IRepository<RolePermission>
{
    /// <summary>
    /// Checks whether a role-permission mapping exists.
    /// </summary>
    Task<bool> ExistsAsync(int roleDefId, int permissionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all role permissions for a specific role definition.
    /// </summary>
    Task<IReadOnlyList<RolePermission>> GetByRoleAsync(int roleDefId, CancellationToken cancellationToken = default);
}
