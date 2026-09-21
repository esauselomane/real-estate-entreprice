using System.Net;
using System.Net.Mail;
using Identity.Microservice.Models;
using Microsoft.Extensions.Options;
using Shared.Presentation.Email;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Sends billing notifications through the shared SMTP configuration.</summary>
public interface IBillingNotificationEmailSender
{
    Task SendAsync(string recipient, string template, CancellationToken cancellationToken);
}

/// <summary>Creates and delivers one billing notification email.</summary>
public sealed class BillingNotificationEmailSender(IOptions<EmailSettings> options) : IBillingNotificationEmailSender
{
    private readonly EmailSettings settings = options.Value;

    public async Task SendAsync(string recipient, string template, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException("SMTP is not configured.");

        var content = new EmailContent("Biscove Real Estate Tech", BillingEmailTemplates.Subject(template), "Hello,", BillingEmailTemplates.Body(template), UseSystemLogo: true);
        using var message = EmailLayout.CreateMessage(settings.FromAddress, recipient, content);
        if (MailAddress.TryCreate(settings.SupportEmail, out var supportAddress))
            message.ReplyToList.Add(supportAddress);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);

        await client.SendMailAsync(message, cancellationToken);
    }
}
