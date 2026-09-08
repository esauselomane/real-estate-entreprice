using Identity.Microservice.DTOs;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions;

/// <summary>
/// Repository contract for <see cref="UserAccessClaim"/> aggregate root.
/// </summary>
public interface IUserAccessClaimRepository : IRepository<UserAccessClaim>
{
    /// <summary>
    /// Retrieves a user access claim by user identifier.
    /// </summary>
    Task<UserAccessClaim?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
