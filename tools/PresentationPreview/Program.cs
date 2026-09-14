using CRM.Microservice.DTOs;
using CRM.Microservice.Services.Brochures;
using Shared.Presentation.Email;

var outputDirectory = Path.GetFullPath(args.FirstOrDefault() ?? "docs/review/previews");
Directory.CreateDirectory(outputDirectory);
var assembly = typeof(EmailLayout).Assembly;
using var logoStream = assembly.GetManifestResourceStream("SystemLogo.jpg")!;
using var logoBytes = new MemoryStream();
logoStream.CopyTo(logoBytes);
var embeddedLogo = "data:image/jpeg;base64," + Convert.ToBase64String(logoBytes.ToArray());

WriteEmail("registration", new EmailContent("Biscove Technologies", "Welcome — set up your account", "Hello Alex,",
    "<p>Your account for <strong>Example Agency</strong> is ready. Set your password to securely access your workspace.</p><p>This invitation expires after 48 hours.</p>",
    "Set your password", "https://example.com/set-password", UseSystemLogo: true));
WriteEmail("password-change", new EmailContent("Biscove Technologies", "Your password has been changed", "Hello Alex,",
    "<p>Your account password was changed successfully.</p><p>If you did not make this change, contact your agency administrator immediately to secure your account.</p>", UseSystemLogo: true));
WriteEmail("agency-reminder", new EmailContent("Example Agency", "Please review the outstanding deal documents", "Hello Alex,",
    "<p>Please review the outstanding documents for deal DEMO-001 before Friday.</p><p>Open your agency workspace to review the details.</p>",
    "Review reminder", "https://example.com/workspace"));
WriteEmail("brochure", new EmailContent("Example Agency", "Your property brochure is ready", "Hello Alex,",
    "<p>The brochure for <strong>Example Client</strong> is ready to review.</p><p>Deal reference: DEMO-001<br>Selected units: 3</p><p>Review the document before sharing it with your client.</p>",
    "Open agency workspace", "https://example.com/workspace"));
var brokers = new List<DealBrokerContactDto>
{
    new("broker-1", "Alex Example", "alex@example.com", "+27 00 000 0001"),
    new("broker-2", "Sam Example with a Longer Name", "sam@example.com", "+27 00 000 0002")
};
File.WriteAllText(Path.Combine(outputDirectory, "brochure-brokers.html"),
    "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><body style=\"font-family:Arial;margin:40px;max-width:760px\"><h1>Example Agency</h1><h2>Your property advisers</h2>" + BrokerCoverHelper.Render(brokers) + "</body></html>");
Console.WriteLine($"Presentation previews written to {outputDirectory}");

void WriteEmail(string name, EmailContent content)
{
    var html = EmailLayout.Render(content).Replace("cid:system-logo", embeddedLogo);
    File.WriteAllText(Path.Combine(outputDirectory, name + ".html"), html);
}
