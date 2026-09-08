using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace CRM.Microservice.Consumers;

public class BrochureCompletedConsumer : CrmKafkaConsumer<BrochureGenerationCompletedEvent>
{
    public BrochureCompletedConsumer(ILogger<BrochureCompletedConsumer> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        : base(logger, serviceProvider, configuration, TopicNames.BrochureGenerationCompleted, "crm-brochure-completed-consumer")
    {
    }

    protected override async Task HandleEventAsync(BrochureGenerationCompletedEvent evt, IServiceProvider serviceProvider, CancellationToken ct)
    {
        var dbContext = serviceProvider.GetRequiredService<Data.CRMDbContext>();

        // Update brochure card status
        var card = await dbContext.BrochureCards
            .FirstOrDefaultAsync(c => c.CardId == evt.BrochureCardId, ct);

        if (card != null)
        {
            card.Status = evt.Success ? Models.BrochureCardStatus.Generated : Models.BrochureCardStatus.Failed;
            card.GeneratedFilePath = evt.PdfUrl ?? string.Empty;
            card.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
        }
        else
        {
            Logger.LogWarning("[BROCHURE CONSUMER]: BrochureCard {CardId} not found for update", evt.BrochureCardId);
        }

        // Create notification
        var notification = new Models.Notification
        {
            TenantId = evt.TenantId,
            UserId = evt.RequestedBy,
            Category = "brochure",
            Title = evt.Success ? "Brochure generated successfully" : "Brochure generation failed",
            Body = evt.Success
                ? $"Your brochure for deal {evt.DealId} is ready."
                : $"Brochure generation failed: {evt.ErrorMessage ?? "Unknown error"}",
            LinkType = "brochure",
            LinkId = evt.BrochureCardId.ToString(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync(ct);

        Logger.LogInformation("[BROCHURE CONSUMER]: Processed completion for DealId={DealId} CardId={CardId} Success={Success}",
            evt.DealId, evt.BrochureCardId, evt.Success);
    }
}
