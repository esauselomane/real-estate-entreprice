using CRM.Microservice.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Presentation.Email;

namespace CRM.Microservice.Services.Implementation
{
    public interface IEmailNotificationService
    {
        Task SendReminderEmailAsync(string toEmail, string toName, string title, string body, string agencyName, string? agencyLogoUrl, CancellationToken cancellationToken = default);
        Task SendBrochureGeneratedEmailAsync(string toEmail, string toName, string brokerName, string dealId, string dealAddress, string generatedAt, string clientName, string marketClass, int unitCount, string downloadUrl, string supportEmail, CancellationToken ct = default, string agencyName = "Your agency", string? agencyLogoUrl = null);
    }

    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly EmailSettings _emailSettings;
        private readonly IEmailDelivery _delivery;

        public EmailNotificationService(IConfiguration configuration, ILogger<EmailNotificationService> logger, IEmailDelivery delivery)
        {
            _configuration = configuration;
            _logger = logger;
            _delivery = delivery;
            _emailSettings = configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
        }

        public async Task SendBrochureGeneratedEmailAsync(string toEmail, string toName, string brokerName, string dealId, string dealAddress, string generatedAt, string clientName, string marketClass, int unitCount, string downloadUrl, string supportEmail, CancellationToken ct = default, string agencyName = "Your agency", string? agencyLogoUrl = null)
        {
            try
            {
                var applicationUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
                var body = $"<p>The brochure for <strong>{Shared.Presentation.Email.EmailLayout.Encode(clientName)}</strong> is ready to review.</p><p>Deal reference: {Shared.Presentation.Email.EmailLayout.Encode(dealId)}<br>Prepared: {Shared.Presentation.Email.EmailLayout.Encode(generatedAt)}<br>Selected units: {unitCount}</p><p>Sign in to your workspace to review and download the document before sharing it with your client.</p>";
                var content = new Shared.Presentation.Email.EmailContent(agencyName, "Your property brochure is ready", $"Hello {toName},", body, "Open agency workspace", applicationUrl, agencyLogoUrl);
                await SendEmailAsync(toEmail, toName, content, ct);
            }
            catch (Exception)
            {
                _logger.LogError("Failed to send brochure generated email to {Email}", toEmail);
                throw;
            }
        }

        public async Task SendReminderEmailAsync(string toEmail, string toName, string title, string body, string agencyName, string? agencyLogoUrl, CancellationToken cancellationToken = default)
        {
            var encodedBody = Shared.Presentation.Email.EmailLayout.Encode(body).Replace("\n", "<br>");
            var content = new Shared.Presentation.Email.EmailContent(agencyName, title, $"Hello {toName},",
                $"<p>{encodedBody}</p><p>Please review this reminder in your agency workspace.</p>",
                "Review reminder", _configuration["Frontend:BaseUrl"], agencyLogoUrl);
            try
            {
                await SendEmailAsync(toEmail, toName, content, cancellationToken);
            }
            catch (Exception)
            {
                _logger.LogError("Reminder email delivery failed for {Recipient}", toEmail);
                throw;
            }
        }

        private Task SendEmailAsync(string toEmail, string toName, EmailContent content, CancellationToken cancellationToken) =>
            _delivery.SendAsync(_emailSettings.FromAddress, toEmail, content, _emailSettings.SupportEmail, cancellationToken);
    }
}
