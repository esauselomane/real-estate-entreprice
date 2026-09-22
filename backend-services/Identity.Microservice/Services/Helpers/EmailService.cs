using Microsoft.Extensions.Options;
using Identity.Microservice.Models;
using Shared.Presentation.Email;

namespace Identity.Microservice.Services;

public interface IEmailService
{
    Task SendPasswordSetupEmailAsync(string toEmail, string toName, string activationLink, string agencyName);
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}

/// <summary>System-branded account email delivery. The logo is embedded from the application's public asset.</summary>
public sealed class EmailService(IOptions<EmailSettings> options, IEmailDelivery delivery) : IEmailService
{
    private readonly EmailSettings settings = options.Value;

    public Task SendPasswordSetupEmailAsync(string toEmail, string toName, string activationLink, string agencyName)
    {
        var body = $"<p>Your account for <strong>{EmailLayout.Encode(agencyName)}</strong> is ready. Set your password to securely access your workspace.</p><p>This invitation expires after 48 hours. If you were not expecting it, contact your agency administrator.</p>";
        return DeliverAsync(toEmail, new EmailContent("Biscove Real Estate Tech", "Welcome — set up your account", $"Hello {toName},", body, "Set your password", activationLink, UseSystemLogo: true));
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlBody) =>
        DeliverAsync(toEmail, new EmailContent("Biscove Real Estate Tech", subject, "Hello,", htmlBody, UseSystemLogo: true));

    private Task DeliverAsync(string recipient, EmailContent content) =>
        delivery.SendAsync(settings.FromAddress, recipient, content, settings.SupportEmail);
}
