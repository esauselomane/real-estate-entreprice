using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace CRM.Microservice.Services.Implementation
{
    public interface IEmailNotificationService
    {
        Task SendBrochureGeneratedEmailAsync(string toEmail, string toName, string brokerName, string dealId, string dealAddress, string generatedAt, string clientName, string marketClass, int unitCount, string downloadUrl, string supportEmail, CancellationToken ct = default);
    }

    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly string _templateRoot;
        private readonly EmailSettings _emailSettings;

        public EmailNotificationService(IConfiguration configuration, ILogger<EmailNotificationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _templateRoot = configuration["Email:TemplatesRootPath"] ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates");
            _emailSettings = configuration.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
        }

        public async Task SendBrochureGeneratedEmailAsync(string toEmail, string toName, string brokerName, string dealId, string dealAddress, string generatedAt, string clientName, string marketClass, int unitCount, string downloadUrl, string supportEmail, CancellationToken ct = default)
        {
            try
            {
                var templatePath = Path.Combine(_templateRoot, "notification", "brochure-generated.html");
                string htmlBody;

                if (File.Exists(templatePath))
                {
                    htmlBody = await File.ReadAllTextAsync(templatePath, ct);
                    htmlBody = htmlBody
                        .Replace("{{broker_name}}", brokerName)
                        .Replace("{{deal_id}}", dealId)
                        .Replace("{{deal_address}}", dealAddress)
                        .Replace("{{generated_at}}", generatedAt)
                        .Replace("{{client_name}}", clientName)
                        .Replace("{{market_class}}", marketClass)
                        .Replace("{{unit_count}}", unitCount.ToString())
                        .Replace("{{download_url}}", downloadUrl)
                        .Replace("{{support_email}}", supportEmail);

                    // Handle conditional market class
                    if (string.IsNullOrEmpty(marketClass))
                        htmlBody = htmlBody.Replace("{{#if market_class}}", "").Replace("{{/if}}", "").Replace("{{market_class}}", "");
                    else
                        htmlBody = htmlBody.Replace("{{#if market_class}}", "").Replace("{{/if}}", "");
                }
                else
                {
                    _logger.LogWarning("Brochure email template not found at {TemplatePath}", templatePath);
                    htmlBody = $"<h1>Brochure Generated</h1><p>Hi {toName},</p><p>Your brochure for deal {dealId} ({dealAddress}) is ready. <a href=\"{downloadUrl}\">Download here</a>.</p>";
                }

                await SendEmailAsync(toEmail, toName, "Brochure Generated — " + dealAddress, htmlBody, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send brochure generated email to {Email}", toEmail);
            }
        }

        private async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_emailSettings.Host))
            {
                _logger.LogInformation("[EMAIL SIMULATION]: {Subject} -> {RecipientName} ({Email})", subject, toName, toEmail);
                return;
            }

            using var message = new MailMessage(_emailSettings.FromAddress, toEmail)
            {
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                From = new MailAddress(_emailSettings.FromAddress, _emailSettings.FromName)
            };
            message.ReplyToList.Add(new MailAddress(_emailSettings.SupportEmail, "Support"));

            using var client = new SmtpClient();
            client.Host = _emailSettings.Host;
            client.Port = _emailSettings.Port;
            client.EnableSsl = _emailSettings.EnableSsl;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            if (!string.IsNullOrEmpty(_emailSettings.Username))
                client.Credentials = new NetworkCredential(_emailSettings.Username, _emailSettings.Password);

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent via SMTP to {Email} ({Name})", toEmail, toName);
        }
    }

    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromAddress { get; set; } = string.Empty;
        public string FromName { get; set; } = "Real Estate Enterprise";
        public string SupportEmail { get; set; } = string.Empty;
        public string TemplatesRootPath { get; set; } = string.Empty;
    }
}