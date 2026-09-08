using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Identity.Microservice.DTOs;
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
        private readonly IHttpClientFactory _httpClientFactory;

        public EmailService(
            IOptions<EmailSettings> settings,
            ILogger<EmailService> logger,
            IWebHostEnvironment env,
            IHttpClientFactory httpClientFactory)
        {
            _settings = settings.Value;
            _logger = logger;
            _env = env;
            _httpClientFactory = httpClientFactory;
        }

        public async Task SendPasswordSetupEmailAsync(string toEmail, string toName, string activationLink, string agencyName)
        {
            try
            {
                var templatePath = Path.Combine(_env.WebRootPath, "EmailTemplates", "PasswordSetup.html");
                string htmlBody;
                if (File.Exists(templatePath))
                {
                    htmlBody = await File.ReadAllTextAsync(templatePath);
                    htmlBody = htmlBody
                        .Replace("{{recipient_name}}", toName)
                        .Replace("{{agency_name}}", agencyName)
                        .Replace("{{activation_link}}", activationLink)
                        .Replace("{{support_email}}", _settings.SupportEmail);
                }
                else
                {
                    _logger.LogWarning("Email template not found at {TemplatePath}, using fallback plain text", templatePath);
                    htmlBody = $"<h1>Welcome to {agencyName}</h1><p>Hi {toName},</p><p>Your agency account is ready. <a href=\"{activationLink}\">Click here to set your password</a>.</p>";
                }

                // If a Mailpit API URL is configured, send via HTTP API (avoids SmtpClient .NET 10 issues)
                if (!string.IsNullOrEmpty(_settings.ApiUrl))
                {
                    await SendViaMailpitApiAsync(toEmail, toName, htmlBody, agencyName);
                    _logger.LogInformation("Password setup email sent via Mailpit API to {Email} ({Name}) for agency {AgencyName}",
                        toEmail, toName, agencyName);
                    return;
                }

                // Fallback: try SMTP if host is configured
                if (!string.IsNullOrEmpty(_settings.Host))
                {
                    await SendViaSmtpAsync(toEmail, toName, htmlBody, agencyName);
                    _logger.LogInformation("Password setup email sent via SMTP to {Email} ({Name}) for agency {AgencyName}",
                        toEmail, toName, agencyName);
                    return;
                }

                _logger.LogInformation("No email provider configured. Using fallback simulation log.");
                await SendFallbackEmailAsync(toEmail, toName, activationLink, agencyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password setup email to {Email}. Using fallback simulation log.", toEmail);
                await SendFallbackEmailAsync(toEmail, toName, activationLink, agencyName);
            }
        }

        private async Task SendViaMailpitApiAsync(string toEmail, string toName, string htmlBody, string agencyName)
        {
            var client = _httpClientFactory.CreateClient("Mailpit");
            var payload = new
            {
                From = new { Name = _settings.FromName, Email = _settings.FromAddress },
                To = new[] { new { Name = toName, Email = toEmail } },
                Subject = $"Welcome to {agencyName} — Set Your Password",
                HTML = htmlBody
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_settings.ApiSendPath ?? "/api/v1/send", content);
            if (!response.IsSuccessStatusCode)
            {
                var respBody = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Mailpit API returned {response.StatusCode}: {respBody}");
            }
        }

        private async Task SendViaSmtpAsync(string toEmail, string toName, string htmlBody, string agencyName)
        {
            using var message = new MailMessage(_settings.FromAddress, toEmail)
            {
                Subject = $"Welcome to {agencyName} — Set Your Password",
                Body = htmlBody,
                IsBodyHtml = true,
                From = new MailAddress(_settings.FromAddress, _settings.FromName)
            };
            message.ReplyToList.Add(new MailAddress(_settings.SupportEmail, "Support"));

            using var client = new System.Net.Mail.SmtpClient();
            client.Host = _settings.Host;
            client.Port = _settings.Port;
            client.EnableSsl = false;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            if (!string.IsNullOrEmpty(_settings.Username))
                client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);

            await client.SendMailAsync(message);
        }

        private async Task SendFallbackEmailAsync(string toEmail, string toName, string activationLink, string agencyName)
        {
            _logger.LogInformation(
                "[EMAIL SIMULATION]: Password setup email -> {RecipientName} ({Email}) | Agency: {AgencyName} | Link: {ActivationLink}",
                toName, toEmail, agencyName, activationLink);
            await Task.CompletedTask;
        }
    }
}
