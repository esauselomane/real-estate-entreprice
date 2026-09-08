using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace CRM.Microservice.Consumers;

public class ImportCompletedConsumer : CrmKafkaConsumer<ImportJobCompletedEvent>
{
    public ImportCompletedConsumer(ILogger<ImportCompletedConsumer> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        : base(logger, serviceProvider, configuration, TopicNames.ImportCompleted, "crm-import-completed-consumer")
    {
    }

    protected override async Task HandleEventAsync(ImportJobCompletedEvent evt, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var notificationRepository = serviceProvider.GetRequiredService<Services.Abstractions.INotificationRepository>();
        var dbContext = serviceProvider.GetRequiredService<Data.CRMDbContext>();

        var title = $"Import {evt.Status}";
        var body = $"Processed {evt.SuccessCount} records successfully with {evt.ErrorCount} errors.";

        var notification = new Models.Notification
        {
            TenantId = evt.TenantId,
            UserId = evt.RequestedBy,
            Category = "import",
            Title = title,
            Body = body,
            LinkType = "import",
            LinkId = evt.JobId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);

        Logger.LogInformation("[IMPORT COMPLETED CONSUMER]: Notification created for job {JobId}", evt.JobId);
    }
}
