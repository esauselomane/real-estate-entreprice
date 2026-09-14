using Identity.Microservice.Services.Abstractions.Payments;

namespace Identity.Microservice.Services.Abstractions.Payments
{
    /// <summary>
    /// Common interface for all payment gateway implementations.
    /// Each gateway (Peach, PayFast, Stripe, etc.) implements this interface.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// Unique identifier for this gateway.
        /// </summary>
        PaymentGateway GatewayType { get; }

        /// <summary>
        /// Create a checkout session for subscription signup.
        /// </summary>
        Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create a billing portal session for subscription management.
        /// </summary>
        Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get or create a customer in the gateway.
        /// </summary>
        Task<CustomerInfo?> GetOrCreateCustomerAsync(string tenantId, string email, string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get subscription details from the gateway.
        /// </summary>
        Task<SubscriptionInfo?> GetSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel a subscription in the gateway.
        /// </summary>
        Task<SubscriptionInfo?> CancelSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate incoming webhook signature.
        /// </summary>
        Task<bool> ValidateWebhookSignatureAsync(HttpRequest request, string payload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parse and normalize a webhook event from the gateway.
        /// </summary>
        WebhookEvent ParseWebhookEvent(string payload);

        /// <summary>
        /// Get the price ID for a pricing package and billing cycle.
        /// </summary>
        string GetPriceId(string pricingPackageCode, BillingCycle billingCycle);
    }
}