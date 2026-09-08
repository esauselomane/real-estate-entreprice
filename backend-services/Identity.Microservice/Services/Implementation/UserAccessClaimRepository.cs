using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="UserAccessClaim"/> aggregate root.
/// </summary>
public class UserAccessClaimRepository : Repository<UserAccessClaim>, IUserAccessClaimRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserAccessClaimRepository"/> class.
    /// </summary>
    public UserAccessClaimRepository(IdentityDbContext dbContext, ILogger<Repository<UserAccessClaim>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<UserAccessClaim?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(claim => claim.UserId == userId, cancellationToken);
    }
}
