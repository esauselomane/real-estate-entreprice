using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace CRM.Microservice.Consumers;

public class NotificationConsumer : CrmKafkaConsumer<NotificationCreatedEvent>
{
    public NotificationConsumer(ILogger<NotificationConsumer> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        : base(logger, serviceProvider, configuration, TopicNames.Notifications, "crm-notification-consumer")
    {
    }

    protected override async Task HandleEventAsync(NotificationCreatedEvent evt, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var notificationRepository = serviceProvider.GetRequiredService<Services.Abstractions.INotificationRepository>();
        var dbContext = serviceProvider.GetRequiredService<Data.CRMDbContext>();

        var notification = new Models.Notification
        {
            TenantId = evt.TenantId,
            UserId = evt.UserId,
            Category = evt.Category,
            Title = evt.Title,
            Body = evt.Body,
            LinkType = evt.LinkType,
            LinkId = evt.LinkId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);

        Logger.LogInformation("[NOTIFICATION CONSUMER]: Created notification for user {UserId} in tenant {TenantId}: {Title}",
            evt.UserId, evt.TenantId, evt.Title);
    }
}
