using Identity.Microservice.Models;
using Identity.Microservice.Data;
using Identity.Microservice.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation
{
    /// <summary>
    /// Repository implementation for <see cref="WebhookEvent"/> aggregate root.
    /// </summary>
    public class WebhookEventRepository : Repository<WebhookEvent>, IWebhookEventRepository
    {
        public WebhookEventRepository(IdentityDbContext dbContext, ILogger<Repository<WebhookEvent>> logger) 
            : base(dbContext, logger)
        {
        }

        public async Task<WebhookEvent?> GetByPeachEventIdAsync(string peachEventId, CancellationToken cancellationToken = default)
        {
            return await _dbSet.AsNoTracking()
                .FirstOrDefaultAsync(w => w.PeachEventId == peachEventId, cancellationToken);
        }

        public async Task<WebhookEvent?> GetUnprocessedByPeachEventIdAsync(string peachEventId, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .FirstOrDefaultAsync(w => w.PeachEventId == peachEventId && !w.Processed, cancellationToken);
        }

        public async Task MarkProcessingAsync(string peachEventId, CancellationToken cancellationToken = default)
        {
            var webhookEvent = await _dbSet.FirstOrDefaultAsync(w => w.PeachEventId == peachEventId, cancellationToken);
            if (webhookEvent != null)
            {
                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _dbContextProtected.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkProcessedAsync(string peachEventId, CancellationToken cancellationToken = default)
        {
            var webhookEvent = await _dbSet.FirstOrDefaultAsync(w => w.PeachEventId == peachEventId, cancellationToken);
            if (webhookEvent != null)
            {
                webhookEvent.Processed = true;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await _dbContextProtected.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task MarkFailedAsync(string peachEventId, string errorMessage, CancellationToken cancellationToken = default)
        {
            var webhookEvent = await _dbSet.FirstOrDefaultAsync(w => w.PeachEventId == peachEventId, cancellationToken);
            if (webhookEvent != null)
            {
                webhookEvent.ErrorMessage = errorMessage;
                await _dbContextProtected.SaveChangesAsync(cancellationToken);
            }
        }
    }
}