using Identity.Microservice.Services.Abstractions.Payments;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Reserved adapter for Peach account onboarding; unsupported operations fail explicitly.</summary>
public sealed class PeachPaymentsGateway(HttpClient client, ILogger<PeachPaymentsGateway> logger) : PaymentGatewayBase(client, logger)
{
    public override bool SupportsPortal => false;
    public override PaymentGateway GatewayType => PaymentGateway.PeachPayments;
    private static NotSupportedException Unavailable() => new("Peach recurring billing is not enabled. Complete Peach product onboarding before enabling this provider.");
    public override Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default) => throw Unavailable();
    public override Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request, CancellationToken cancellationToken = default) => throw Unavailable();
    public override Task<CustomerInfo?> GetOrCreateCustomerAsync(string tenantId, string email, string name, CancellationToken cancellationToken = default) => throw Unavailable();
    public override Task<SubscriptionInfo?> GetSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default) => throw Unavailable();
    public override Task<SubscriptionInfo?> CancelSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default) => throw Unavailable();
    public override Task<bool> ValidateWebhookSignatureAsync(HttpRequest request, string payload, CancellationToken cancellationToken = default) => Task.FromResult(false);
    public override WebhookEvent ParseWebhookEvent(string payload) => throw Unavailable();
    public override string GetPriceId(string pricingPackageCode, BillingCycle billingCycle) => throw Unavailable();
}
