using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="SystemRoleDefinition"/> aggregate root.
/// </summary>
public interface ISystemRoleRepository : IRepository<SystemRoleDefinition>
{
    /// <summary>
    /// Retrieves a system role by its name.
    /// </summary>
    Task<SystemRoleDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
