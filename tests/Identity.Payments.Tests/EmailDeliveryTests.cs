using System.Net;
using System.Text.Json;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Resend;
using Shared.Presentation.Email;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class EmailDeliveryTests
{
    [Fact]
    public void MissingApiTokenFailsConfigurationValidation()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransactionalEmail(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ResendClientOptions>>().Value);
    }

    [Fact]
    public async Task PasswordSetupIncludesBrandingLinkReplyToAndInlineLogo()
    {
        using var handler = new ResendTestHandler();
        using var provider = CreateProvider(handler);
        var service = new EmailService(Options.Create(CreateSettings()), provider.GetRequiredService<IEmailDelivery>());
        await service.SendPasswordSetupEmailAsync("recipient@example.com", "Esau", "https://app.example.com/set-password?token=test", "Acme & Co");
        using var document = JsonDocument.Parse(handler.Body!);
        var email = document.RootElement;
        Assert.Equal("/emails", handler.RequestPath);
        Assert.Equal("Bearer test-api-key", handler.Authorization);
        Assert.Equal("recipient@example.com", email.GetProperty("to")[0].GetString());
        Assert.Equal("Welcome — set up your account", email.GetProperty("subject").GetString());
        Assert.Contains("Acme &amp; Co", email.GetProperty("html").GetString());
        Assert.Contains("https://app.example.com/set-password?token=test", email.GetProperty("html").GetString());
        Assert.Contains("cid:system-logo", email.GetProperty("html").GetString());
        Assert.Contains("Esau", email.GetProperty("text").GetString());
        Assert.Equal("support@example.com", email.GetProperty("reply_to")[0].GetString());
        var attachment = email.GetProperty("attachments")[0];
        Assert.Equal("system-logo", attachment.GetProperty("content_id").GetString());
        Assert.NotEmpty(Convert.FromBase64String(attachment.GetProperty("content").GetString()!));
    }

    [Fact]
    public async Task PasswordResetUsesResend()
    {
        using var handler = new ResendTestHandler();
        using var provider = CreateProvider(handler);
        var service = new EmailService(Options.Create(CreateSettings()), provider.GetRequiredService<IEmailDelivery>());
        await service.SendEmailAsync("recipient@example.com", "Reset your password", "<p>Reset link</p>");
        using var document = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Reset your password", document.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task BillingUsesResendAndSelectedTemplate()
    {
        using var handler = new ResendTestHandler();
        using var provider = CreateProvider(handler);
        var service = new BillingNotificationEmailSender(Options.Create(CreateSettings()), provider.GetRequiredService<IEmailDelivery>());
        await service.SendAsync("billing@example.com", "trial-ending", CancellationToken.None);
        using var document = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Your free trial ends soon", document.RootElement.GetProperty("subject").GetString());
        Assert.Contains("free trial ends within three days", document.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public async Task PaymentApprovalEmailConfirmsActiveAccount()
    {
        using var handler = new ResendTestHandler();
        using var provider = CreateProvider(handler);
        var sender = new BillingNotificationEmailSender(Options.Create(CreateSettings()), provider.GetRequiredService<IEmailDelivery>());
        await sender.SendAsync("recipient@example.com", "payment-approved", CancellationToken.None);
        using var document = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Payment approved — your account is active", document.RootElement.GetProperty("subject").GetString());
        Assert.Contains("payment has been approved", document.RootElement.GetProperty("html").GetString());
        Assert.Contains("agency account is active", document.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public async Task ProviderRejectionDoesNotReportSuccess()
    {
        using var handler = new ResendTestHandler(HttpStatusCode.Forbidden);
        using var provider = CreateProvider(handler);
        var service = new EmailService(Options.Create(CreateSettings()), provider.GetRequiredService<IEmailDelivery>());
        await Assert.ThrowsAnyAsync<Exception>(() => service.SendEmailAsync("recipient@example.com", "Test", "<p>Test</p>"));
        Assert.NotNull(handler.Body);
    }

    [Fact]
    public async Task CrmReminderUsesAgencyBrandAndPropagatesProviderFailure()
    {
        using var handler = new ResendTestHandler();
        using var provider = CreateProvider(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:FromAddress"] = "sender@example.com", ["Email:SupportEmail"] = "support@example.com",
            ["Frontend:BaseUrl"] = "https://app.example.com"
        }).Build();
        var service = new CRM.Microservice.Services.Implementation.EmailNotificationService(configuration,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CRM.Microservice.Services.Implementation.EmailNotificationService>.Instance,
            provider.GetRequiredService<IEmailDelivery>());
        await service.SendReminderEmailAsync("recipient@example.com", "Esau", "Reminder", "<untrusted>", "Agency & Co", "https://example.com/logo.png");
        using var document = JsonDocument.Parse(handler.Body!);
        Assert.Contains("Agency &amp; Co", document.RootElement.GetProperty("html").GetString());
        Assert.Contains("&lt;untrusted&gt;", document.RootElement.GetProperty("html").GetString());
        handler.StatusCode = HttpStatusCode.Forbidden;
        await Assert.ThrowsAnyAsync<Exception>(() => service.SendReminderEmailAsync("recipient@example.com", "Esau", "Reminder", "Body", "Agency", null));
    }

    private static ServiceProvider CreateProvider(ResendTestHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Resend:ApiToken"] = "test-api-key" }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransactionalEmail(configuration);
        services.AddResend("test-api-key").ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static EmailSettings CreateSettings() => new() { FromAddress = "sender@example.com", SupportEmail = "support@example.com" };
}
