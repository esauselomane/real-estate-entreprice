using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Presentation.Email;

/// <summary>Reusable, email-client-friendly layout with encoded text, validated links and plain-text alternatives.</summary>
public static class EmailLayout
{
    public static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    public static string? SafeUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri.AbsoluteUri : null;

    public static string Render(EmailContent content)
    {
        var logo = content.UseSystemLogo ? "cid:system-logo" : SafeUrl(content.LogoUrl);
        var logoHtml = logo == null ? string.Empty : $"<img src=\"{Encode(logo)}\" alt=\"{Encode(content.BrandName)}\" width=\"150\" style=\"display:block;max-width:150px;height:auto;margin-bottom:20px\">";
        var url = SafeUrl(content.ActionUrl);
        var action = url == null ? string.Empty : $"<p style=\"margin:28px 0\"><a href=\"{Encode(url)}\" style=\"background:#185abd;color:#ffffff;text-decoration:none;padding:14px 24px;display:inline-block;border-radius:6px;font-weight:bold\">{Encode(content.ActionLabel ?? "View details")}</a></p><p style=\"font-size:12px;overflow-wrap:anywhere\">If the button does not work, copy this link into your browser:<br>{Encode(url)}</p>";
        return $"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#f1f5f9;font-family:Arial,Helvetica,sans-serif;color:#334155">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:32px 12px">
            <table role="presentation" width="600" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;background:#ffffff;border:1px solid #e2e8f0;border-radius:10px">
            <tr><td style="padding:32px">{logoHtml}<p style="font-size:12px;letter-spacing:1px;color:#64748b">{Encode(content.BrandName)}</p>
            <h1 style="font-size:24px;line-height:32px;color:#0f172a;margin:16px 0 24px">{Encode(content.Subject)}</h1>
            <p style="line-height:24px">{Encode(content.Greeting)}</p><div style="font-size:15px;line-height:25px">{content.BodyHtml}</div>{action}
            <p style="margin-top:28px">Kind regards,<br><strong>{Encode(content.BrandName)}</strong></p></td></tr>
            <tr><td style="padding:20px 32px;background:#f8fafc;font-size:12px;line-height:20px;color:#64748b">This message relates to your account or agency activity. Please contact your agency administrator if you need assistance.</td></tr>
            </table></td></tr></table></body></html>
            """;
    }

    public static MailMessage CreateMessage(string from, string recipient, EmailContent content)
    {
        var html = Render(content);
        var plainText = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        var message = new MailMessage(from, recipient) { Subject = content.Subject, From = new MailAddress(from, content.BrandName) };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plainText, Encoding.UTF8, MediaTypeNames.Text.Plain));
        var view = AlternateView.CreateAlternateViewFromString(html, Encoding.UTF8, MediaTypeNames.Text.Html);
        if (content.UseSystemLogo)
        {
            var stream = typeof(EmailLayout).Assembly.GetManifestResourceStream("SystemLogo.jpg")!;
            view.LinkedResources.Add(new LinkedResource(stream, MediaTypeNames.Image.Jpeg) { ContentId = "system-logo", TransferEncoding = TransferEncoding.Base64 });
        }
        message.AlternateViews.Add(view);
        return message;
    }
}
