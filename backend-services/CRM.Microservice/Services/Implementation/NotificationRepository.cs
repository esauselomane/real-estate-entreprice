using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Data;
using CRM.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Repository implementation for <see cref="Notification"/> aggregate root.
/// </summary>
public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationRepository"/> class.
    /// </summary>
    public NotificationRepository(CRMDbContext dbContext, ILogger<Repository<Notification>> logger) : base(dbContext, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Notification>> GetByTenantIdAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(notification => notification.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Notification>> GetUnreadAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(notification => notification.TenantId == tenantId && !notification.IsRead)
            .ToListAsync(cancellationToken);
    }
}
