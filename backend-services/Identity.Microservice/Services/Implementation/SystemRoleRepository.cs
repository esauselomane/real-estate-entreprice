using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="SystemRoleDefinition"/> aggregate root.
/// </summary>
public class SystemRoleRepository : Repository<SystemRoleDefinition>, ISystemRoleRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemRoleRepository"/> class.
    /// </summary>
    public SystemRoleRepository(IdentityDbContext dbContext, ILogger<Repository<SystemRoleDefinition>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<SystemRoleDefinition?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(role => role.Name == name, cancellationToken);
    }
}
