using System.Net;
using System.Security.Claims;
using Identity.Microservice.Configuration;
using Identity.Microservice.Controllers;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions.Payments;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class PaymentTests
{
    [Fact]
    public void ExpiredTrialRequiresUpgradeButActivePaidDoesNot()
    {
        var now = DateTime.UtcNow;
        var tenant = new TenantAgency { IsTrial = true, TrialEndDate = now.AddSeconds(-1) };
        Assert.True(PaymentStateHelper.Describe(tenant, null, null, now).RequiresUpgrade);
        var subscription = new Subscription { Status = SubscriptionStatus.Active, CurrentPeriodEnd = now.AddMonths(1) };
        Assert.False(PaymentStateHelper.Describe(tenant, subscription, null, now).RequiresUpgrade);
        subscription.Status = SubscriptionStatus.Cancelled;
        Assert.False(PaymentStateHelper.Describe(tenant, subscription, null, now).RequiresUpgrade);
    }

    [Fact]
    public async Task AnnualCheckoutUsesDatabaseAmountAndReusesIdempotencyKey()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.Checkout;
        var key = Guid.NewGuid().ToString();
        var first = await fixture.Service.CreateCheckoutSessionAsync(request, key);
        var second = await fixture.Service.CreateCheckoutSessionAsync(request, key);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal("POST", first.Method);
        Assert.Equal("2990.00", first.FormFields!["amount"]);
        Assert.Equal("6", first.FormFields["frequency"]);
        Assert.Equal("1", first.FormFields["subscription_type"]);
        Assert.Equal(1, await fixture.Database.PaymentIntents.CountAsync());
        Assert.True((await fixture.Service.GetSubscriptionStatusAsync("tenant-a")).IsTrial);
    }

    [Fact]
    public async Task WebhookActivationIsPersistedAndDuplicateDoesNotExtendTwice()
    {
        await using var fixture = await Fixture.CreateAsync();
        var checkout = await fixture.Service.CreateCheckoutSessionAsync(fixture.Checkout, Guid.NewGuid().ToString());
        var payload = $"m_payment_id={checkout.SessionId}&pf_payment_id=payment-1&payment_status=COMPLETE&token=subscription-1&amount_gross=2990.00";
        var paymentEvent = fixture.Gateway.ParseWebhookEvent(payload);
        await fixture.Service.ProcessWebhookEventAsync(paymentEvent, payload);
        fixture.Database.ChangeTracker.Clear();
        var first = await fixture.Service.GetSubscriptionStatusAsync("tenant-a");
        await fixture.Service.ProcessWebhookEventAsync(paymentEvent, payload);
        fixture.Database.ChangeTracker.Clear();
        var second = await fixture.Service.GetSubscriptionStatusAsync("tenant-a");
        Assert.Equal("active", first.Status);
        Assert.Equal(PaymentGateway.PayFast, first.Gateway);
        Assert.Equal(first.CurrentPeriodEnd, second.CurrentPeriodEnd);
        Assert.Single(await fixture.Database.WebhookEvents.ToListAsync());
        var notification = Assert.Single(await fixture.Database.BillingNotifications.ToListAsync());
        Assert.Equal("payment-approved", notification.Template);
        Assert.Equal("test@example.com", notification.Recipient);
        Assert.False((await fixture.Database.TenantAgencies.SingleAsync()).IsTrial);
    }

    [Fact]
    public async Task WrongAmountRollsBackAndLeavesReviewableFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        var checkout = await fixture.Service.CreateCheckoutSessionAsync(fixture.Checkout, Guid.NewGuid().ToString());
        var payload = $"m_payment_id={checkout.SessionId}&pf_payment_id=payment-1&payment_status=COMPLETE&token=subscription-1&amount_gross=1.00";
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.ProcessWebhookEventAsync(fixture.Gateway.ParseWebhookEvent(payload), payload));
        Assert.Empty(await fixture.Database.Subscriptions.ToListAsync());
        Assert.Empty(await fixture.Database.BillingNotifications.ToListAsync());
        var item = await fixture.Database.WebhookEvents.SingleAsync();
        Assert.False(item.Processed);
        Assert.Contains("amount", item.ErrorMessage);
    }

    [Fact]
    public async Task CrossTenantReadDeniedAndCheckoutUsesTenantClaim()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = new PaymentsController(fixture.Service, fixture.Factory, fixture.Database)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("TenantId", "tenant-a"), new Claim(ClaimTypes.NameIdentifier, "user-a"),
            new Claim(ClaimTypes.Role, "AgencyAdmin")
        }, "test"));
        Assert.IsType<ForbidResult>(await controller.GetSubscriptionStatus("tenant-b"));
        Assert.IsType<OkObjectResult>(await controller.GetSubscriptionStatus());
        Assert.IsType<OkObjectResult>(await controller.CreateCheckoutSession(new CreateCheckoutSessionRequest
        {
            PricingPackageCode = "SME_AGENCY", BillingCycle = "annual", IdempotencyKey = Guid.NewGuid().ToString(),
            SuccessUrl = fixture.Checkout.SuccessUrl, CancelUrl = fixture.Checkout.CancelUrl
        }));
        Assert.Equal("tenant-a", (await fixture.Database.PaymentIntents.SingleAsync()).TenantId);
    }

    [Fact]
    public async Task InvalidSignatureIsRejectedWithoutProviderPostback()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.False(await fixture.Gateway.ValidateWebhookSignatureAsync(new DefaultHttpContext().Request, "merchant_id=test&signature=wrong"));
        Assert.Equal(0, fixture.Handler.Calls);
    }

    [Fact]
    public async Task ValidSignatureRequiresProviderValidationEvenAfterBodyIsConsumed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var fields = new Dictionary<string, string> { ["merchant_id"] = "test", ["payment_status"] = "COMPLETE", ["m_payment_id"] = "reference" };
        fields["signature"] = PayFastSignatureHelper.Sign(fields, "passphrase");
        var payload = await new FormUrlEncodedContent(fields).ReadAsStringAsync();
        var request = new DefaultHttpContext().Request;
        request.Body = new MemoryStream();
        Assert.True(await fixture.Gateway.ValidateWebhookSignatureAsync(request, payload));
        Assert.Equal(1, fixture.Handler.Calls);
        fixture.Handler.Valid = false;
        Assert.False(await fixture.Gateway.ValidateWebhookSignatureAsync(request, payload));
    }

    [Fact]
    public async Task EnterpriseAndUntrustedReturnUrlAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateCheckoutSessionAsync(fixture.Checkout with { PricingPackageCode = "ENTERPRISE" }, Guid.NewGuid().ToString()));
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateCheckoutSessionAsync(fixture.Checkout with { SuccessUrl = "https://attacker.example" }, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task ExistingTrialExtensionIsOnceOnlyAndExcludesPaidTenants()
    {
        await using var fixture = await Fixture.CreateAsync();
        var original = (await fixture.Database.TenantAgencies.SingleAsync()).TrialEndDate!.Value;
        await TrialExtensionHelper.ApplyAsync(fixture.Database);
        await TrialExtensionHelper.ApplyAsync(fixture.Database);
        Assert.Equal(original.AddMonths(2), (await fixture.Database.TenantAgencies.SingleAsync()).TrialEndDate);
        fixture.Database.TenantAgencies.Add(new TenantAgency { TenantId = "paid", IsTrial = true, TrialEndDate = original });
        fixture.Database.Subscriptions.Add(new Subscription { TenantId = "paid", PeachSubscriptionId = "paid-token", Status = SubscriptionStatus.Active });
        await fixture.Database.SaveChangesAsync();
        await TrialExtensionHelper.ApplyAsync(fixture.Database);
        Assert.Equal(original, (await fixture.Database.TenantAgencies.SingleAsync(tenant => tenant.TenantId == "paid")).TrialEndDate);
    }

    [Fact]
    public async Task FailedPaymentAndRecoveryRestoreAccessWithoutDuplicatePeriodExtension()
    {
        await using var fixture = await Fixture.CreateAsync();
        var checkout = await fixture.Service.CreateCheckoutSessionAsync(fixture.Checkout, Guid.NewGuid().ToString());
        var success = $"m_payment_id={checkout.SessionId}&pf_payment_id=first&payment_status=COMPLETE&token=token-1&amount_gross=2990.00";
        await fixture.Service.ProcessWebhookEventAsync(fixture.Gateway.ParseWebhookEvent(success), success);
        var failure = $"m_payment_id={checkout.SessionId}&pf_payment_id=second&payment_status=FAILED&token=token-1";
        await fixture.Service.ProcessWebhookEventAsync(fixture.Gateway.ParseWebhookEvent(failure), failure);
        Assert.True((await fixture.Service.GetSubscriptionStatusAsync("tenant-a")).RequiresUpgrade);
        var recovered = $"m_payment_id={checkout.SessionId}&pf_payment_id=second&payment_status=COMPLETE&token=token-1&amount_gross=2990.00";
        await fixture.Service.ProcessWebhookEventAsync(fixture.Gateway.ParseWebhookEvent(recovered), recovered);
        Assert.False((await fixture.Service.GetSubscriptionStatusAsync("tenant-a")).RequiresUpgrade);
        // A late retry of the failure must not undo payment recovery.
        await fixture.Service.ProcessWebhookEventAsync(fixture.Gateway.ParseWebhookEvent(failure), failure);
        Assert.Equal("active", (await fixture.Service.GetSubscriptionStatusAsync("tenant-a")).Status);
    }

    [Fact]
    public async Task SandboxPortalIsExplicitlyUnavailable()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.False(fixture.Gateway.SupportsPortal);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Gateway.CreatePortalSessionAsync(new CreatePortalRequest { GatewaySubscriptionId = "token" }));
    }

    private sealed class ValidationHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public bool Valid { get; set; } = true;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Valid ? "VALID" : "INVALID") });
        }
    }

    [Fact]
    public async Task ApprovedRenewalQueuesAnotherConfirmationButDuplicateDoesNot()
    {
        await using var fixture = await Fixture.CreateAsync();
        var checkout = await fixture.Service.CreateCheckoutSessionAsync(fixture.Checkout, Guid.NewGuid().ToString());
        var firstPayload = $"m_payment_id={checkout.SessionId}&pf_payment_id=first&payment_status=COMPLETE&token=subscription-1&amount_gross=2990.00";
        await fixture.Service.ProcessWebhookEventAsync(fixture.Gateway.ParseWebhookEvent(firstPayload), firstPayload);
        var renewalPayload = $"m_payment_id={checkout.SessionId}&pf_payment_id=renewal&payment_status=COMPLETE&token=subscription-1&amount_gross=2990.00";
        var renewalEvent = fixture.Gateway.ParseWebhookEvent(renewalPayload);
        await fixture.Service.ProcessWebhookEventAsync(renewalEvent, renewalPayload);
        await fixture.Service.ProcessWebhookEventAsync(renewalEvent, renewalPayload);
        var notifications = await fixture.Database.BillingNotifications.ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, notification => Assert.Equal("payment-approved", notification.Template));
        Assert.Equal(SubscriptionStatus.Active, (await fixture.Database.TenantAgencies.SingleAsync()).SubscriptionStatus);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public IdentityDbContext Database { get; private set; } = null!;
        public ValidationHandler Handler { get; } = new();
        public PayFastGateway Gateway { get; private set; } = null!;
        public PaymentGatewayFactory Factory { get; private set; } = null!;
        public PaymentOrchestrationService Service { get; private set; } = null!;
        public CreateCheckoutRequest Checkout => new()
        {
            TenantId = "tenant-a", PricingPackageCode = "SME_AGENCY", BillingCycle = BillingCycle.Annual,
            SuccessUrl = "http://localhost:5173/?billing=1&paymentStatus=success", CancelUrl = "http://localhost:5173/?billing=1"
        };

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await fixture.connection.OpenAsync();
            fixture.Database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(fixture.connection).Options);
            await fixture.Database.Database.EnsureCreatedAsync();
            fixture.Database.TenantAgencies.Add(new TenantAgency { TenantId = "tenant-a", AgencyName = "Test", ContactEmail = "test@example.com", IsTrial = true, TrialEndDate = DateTime.UtcNow.AddDays(14) });
            fixture.Database.PricingPackages.Add(new PricingPackage { Code = "SME_AGENCY", Name = "SME Agency", MonthlyPrice = 299, AnnualPrice = 2990, IsActive = true });
            await fixture.Database.SaveChangesAsync();
            fixture.Gateway = new PayFastGateway(new HttpClient(fixture.Handler), Options.Create(new PayFastSettings { MerchantId = "test", MerchantKey = "key", PassPhrase = "passphrase", WebhookUrl = "https://app.example/gateway/payments/webhook/payfast" }), NullLogger<PayFastGateway>.Instance);
            var settings = Options.Create(new PaymentSettings());
            fixture.Factory = new PaymentGatewayFactory(new[] { fixture.Gateway }, settings);
            fixture.Service = new PaymentOrchestrationService(fixture.Database, fixture.Factory, settings, NullLogger<PaymentOrchestrationService>.Instance);
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
