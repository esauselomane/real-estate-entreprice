using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="Permission"/> aggregate root.
/// </summary>
public interface IPermissionRepository : IRepository<Permission>
{
    /// <summary>
    /// Retrieves a permission by its key.
    /// </summary>
    Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}
