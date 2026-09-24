using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Resend;

namespace Shared.Presentation.Email;

/// <summary>Submits transactional messages to Resend and records provider acceptance.</summary>
public sealed class ResendEmailDelivery(IResend client, ILogger<ResendEmailDelivery> logger) : IEmailDelivery
{
    public async Task SendAsync(string fromAddress, string recipient, EmailContent content, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            From = new MailAddress(fromAddress, content.BrandName).ToString(),
            Subject = content.Subject,
            HtmlBody = EmailLayout.Render(content),
            TextBody = EmailLayout.RenderPlainText(content),
            ReplyTo = [],
            Attachments = []
        };
        message.To.Add(recipient);
        if (!string.IsNullOrWhiteSpace(replyTo))
            message.ReplyTo.Add(new MailAddress(replyTo).Address);

        if (content.UseSystemLogo)
        {
            await using var logoStream = typeof(EmailLayout).Assembly.GetManifestResourceStream("SystemLogo.jpg")
                ?? throw new InvalidOperationException("The system email logo is missing.");
            using var logoBuffer = new MemoryStream();
            await logoStream.CopyToAsync(logoBuffer, cancellationToken);
            message.Attachments.Add(new EmailAttachment
            {
                Filename = "logo.jpg",
                Content = logoBuffer.ToArray(),
                ContentType = "image/jpeg",
                ContentId = "system-logo"
            });
        }

        ResendResponse<Guid> response;
        try
        {
            response = await client.EmailSendAsync(message, cancellationToken);
        }
        catch (ResendException exception)
        {
            logger.LogError("Resend submission failed: HTTP {StatusCode}, category {ErrorType}.", exception.StatusCode, exception.ErrorType);
            throw;
        }
        if (!response.Success)
        {
            logger.LogError("Resend rejected transactional email submission: HTTP {StatusCode}, category {ErrorType}.",
                response.Exception?.StatusCode, response.Exception?.ErrorType);
            throw new InvalidOperationException("The email provider rejected the message.");
        }
        logger.LogInformation("Resend accepted email {EmailId} for delivery", response.Content);
    }
}
