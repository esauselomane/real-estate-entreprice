using Identity.Microservice.Models;
using Microsoft.Extensions.Options;
using Shared.Presentation.Email;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Sends billing notifications through the shared Resend delivery service.</summary>
public interface IBillingNotificationEmailSender
{
    Task SendAsync(string recipient, string template, CancellationToken cancellationToken);
}

/// <summary>Creates and delivers one billing notification email.</summary>
public sealed class BillingNotificationEmailSender(IOptions<EmailSettings> options, IEmailDelivery delivery) : IBillingNotificationEmailSender
{
    private readonly EmailSettings settings = options.Value;

    public Task SendAsync(string recipient, string template, CancellationToken cancellationToken)
    {
        var content = new EmailContent("Biscove Real Estate Tech", BillingEmailTemplates.Subject(template), "Hello,", BillingEmailTemplates.Body(template), UseSystemLogo: true);
        return delivery.SendAsync(settings.FromAddress, recipient, content, settings.SupportEmail, cancellationToken);
    }
}
