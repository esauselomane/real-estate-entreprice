using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CRM.Microservice.Services.Implementation;
using Shared.Kafka.Events;
using Shared.Kafka.Topics;

namespace CRM.Microservice.Consumers;

public class BrochureCompletedConsumer : CrmKafkaConsumer<BrochureGenerationCompletedEvent>
{
    private readonly IConfiguration _configuration;
    private readonly EmailSettings _emailSettings;

    public BrochureCompletedConsumer(ILogger<BrochureCompletedConsumer> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        : base(logger, serviceProvider, configuration, TopicNames.BrochureGenerationCompleted, "crm-brochure-completed-consumer")
    {
        _configuration = configuration;
        _emailSettings = configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
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

        // Create in-app notification
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

        // Send email notification if successful
        if (evt.Success)
        {
            var emailService = serviceProvider.GetService<IEmailNotificationService>();
            if (emailService != null)
            {
                try
                {
                    // Get broker email and name from the broker repository
                    var brokerRepo = serviceProvider.GetRequiredService<Services.Abstractions.IBrokerRepository>();
                    var broker = await brokerRepo.GetByIdAsync(evt.RequestedBy, ct);
                    if (broker != null && !string.IsNullOrEmpty(broker.Email))
                    {
                        // Get deal to access property address
                        var deal = await dbContext.DealRecords.FirstOrDefaultAsync(d => d.DealId == evt.DealId, ct);
                        
                        var downloadUrl = $"{_configuration["Gateway:BaseUrl"]}/api/crm/brochure-cards/{evt.BrochureCardId}/download";
                        await emailService.SendBrochureGeneratedEmailAsync(
                            toEmail: broker.Email,
                            toName: broker.FullName,
                            brokerName: broker.FullName,
                            dealId: evt.DealId.ToString(),
                            dealAddress: deal?.PropertyAddress ?? $"Deal #{evt.DealId}",
                            generatedAt: DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"),
                            clientName: card?.ClientName ?? "Client",
                            marketClass: "",
                            unitCount: card?.Lines?.Count ?? 0,
                            downloadUrl: downloadUrl,
                            supportEmail: _emailSettings?.SupportEmail ?? "support@realestate.com",
                            ct: ct);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to send brochure email notification for DealId={DealId}", evt.DealId);
                }
            }
        }

        Logger.LogInformation("[BROCHURE CONSUMER]: Processed completion for DealId={DealId} CardId={CardId} Success={Success}",
            evt.DealId, evt.BrochureCardId, evt.Success);
    }
}
