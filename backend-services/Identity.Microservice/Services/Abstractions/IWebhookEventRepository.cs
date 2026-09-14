using Identity.Microservice.Models;

namespace Identity.Microservice.Services.Abstractions
{
    /// <summary>
    /// Repository contract for <see cref="WebhookEvent"/> aggregate root.
    /// </summary>
    public interface IWebhookEventRepository : IRepository<WebhookEvent>
    {
        Task<WebhookEvent?> GetByPeachEventIdAsync(string peachEventId, CancellationToken cancellationToken = default);
        Task<WebhookEvent?> GetUnprocessedByPeachEventIdAsync(string peachEventId, CancellationToken cancellationToken = default);
        Task MarkProcessingAsync(string peachEventId, CancellationToken cancellationToken = default);
        Task MarkProcessedAsync(string peachEventId, CancellationToken cancellationToken = default);
        Task MarkFailedAsync(string peachEventId, string errorMessage, CancellationToken cancellationToken = default);
    }
}