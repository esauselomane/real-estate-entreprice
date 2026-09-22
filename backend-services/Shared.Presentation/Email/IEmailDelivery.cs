namespace Shared.Presentation.Email;

/// <summary>Delivers a rendered transactional email through the configured provider.</summary>
public interface IEmailDelivery
{
    Task SendAsync(string fromAddress, string recipient, EmailContent content, string? replyTo = null, CancellationToken cancellationToken = default);
}
