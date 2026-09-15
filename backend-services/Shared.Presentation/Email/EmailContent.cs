namespace Shared.Presentation.Email;

public sealed record EmailContent(string BrandName, string Subject, string Greeting, string BodyHtml,
    string? ActionLabel = null, string? ActionUrl = null, string? LogoUrl = null, bool UseSystemLogo = false);
