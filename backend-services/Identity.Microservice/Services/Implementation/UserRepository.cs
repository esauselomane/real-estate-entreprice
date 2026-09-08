using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="UserAccount"/> aggregate root.
/// </summary>
public class UserRepository : Repository<UserAccount>, IUserRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    public UserRepository(IdentityDbContext dbContext, ILogger<Repository<UserAccount>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserAccount>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(user => user.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }
}
