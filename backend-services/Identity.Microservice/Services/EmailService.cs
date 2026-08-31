using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Identity.Microservice.Models;

namespace Identity.Microservice.Services
{
    public interface IEmailService
    {
        Task SendPasswordSetupEmailAsync(string toEmail, string toName, string activationLink, string agencyName);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;
        private readonly IWebHostEnvironment _env;

        public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger, IWebHostEnvironment env)
        {
            _settings = settings.Value;
            _logger = logger;
            _env = env;
        }

        public async Task SendPasswordSetupEmailAsync(string toEmail, string toName, string activationLink, string agencyName)
        {
            try
            {
                var templatePath = Path.Combine(_env.WebRootPath, "EmailTemplates", "PasswordSetup.html");
                if (!File.Exists(templatePath))
                {
                    _logger.LogWarning("Email template not found at {TemplatePath}, using fallback plain text", templatePath);
                    await SendFallbackEmailAsync(toEmail, toName, activationLink, agencyName);
                    return;
                }

                var htmlBody = await File.ReadAllTextAsync(templatePath);
                htmlBody = htmlBody
                    .Replace("{{recipient_name}}", toName)
                    .Replace("{{agency_name}}", agencyName)
                    .Replace("{{activation_link}}", activationLink)
                    .Replace("{{support_email}}", _settings.SupportEmail);

                using var message = new MailMessage(_settings.FromAddress, toEmail)
                {
                    Subject = $"Welcome to {agencyName} — Set Your Password",
                    Body = htmlBody,
                    IsBodyHtml = true,
                    From = new MailAddress(_settings.FromAddress, _settings.FromName)
                };
                message.ReplyToList.Add(new MailAddress(_settings.SupportEmail, "Support"));

                using var client = new SmtpClient(_settings.Host, _settings.Port)
                {
                    EnableSsl = _settings.EnableSsl,
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password)
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Password setup email sent to {Email} ({Name}) for agency {AgencyName}",
                    toEmail, toName, agencyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password setup email to {Email}. Using fallback simulation log.", toEmail);
                await SendFallbackEmailAsync(toEmail, toName, activationLink, agencyName);
            }
        }

        private async Task SendFallbackEmailAsync(string toEmail, string toName, string activationLink, string agencyName)
        {
            _logger.LogInformation(
                "[EMAIL SIMULATION]: Password setup email → {RecipientName} ({Email}) | Agency: {AgencyName} | Link: {ActivationLink}",
                toName, toEmail, agencyName, activationLink);
            await Task.CompletedTask;
        }
    }
}
