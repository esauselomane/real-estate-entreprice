namespace Identity.Microservice.Services.Abstractions.Payments
{
    /// <summary>
    /// Supported payment gateway providers.
    /// </summary>
    public enum PaymentGateway
    {
        PeachPayments = 1,
        PayFast = 2
    }

    /// <summary>
    /// Billing cycle for subscriptions.
    /// </summary>
    public enum BillingCycle
    {
        Monthly = 1,
        Annual = 2
    }

    /// <summary>
    /// Subscription status normalized across all gateways.
    /// </summary>
    public enum SubscriptionStatus
    {
        Trialing = 0,
        Active = 1,
        PastDue = 2,
        Cancelled = 3,
        Expired = 4,
        Paused = 5
    }

    /// <summary>
    /// Request to create a checkout session for subscription signup.
    /// </summary>
    public record CreateCheckoutRequest
    {
        public string TenantId { get; init; } = string.Empty;
        public string PricingPackageCode { get; init; } = string.Empty;
        public BillingCycle BillingCycle { get; init; } = BillingCycle.Monthly;
        public string SuccessUrl { get; init; } = string.Empty;
        public string CancelUrl { get; init; } = string.Empty;
        public string? CustomerEmail { get; init; }
        public string? CustomerName { get; init; }
        public PaymentGateway? PreferredGateway { get; init; }
    }

    /// <summary>
    /// Result of creating a checkout session.
    /// </summary>
    public record CreateCheckoutResult
    {
        public string CheckoutUrl { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public PaymentGateway Gateway { get; init; }
    }

    /// <summary>
    /// Request to create a billing portal session.
    /// </summary>
    public record CreatePortalRequest
    {
        public string TenantId { get; init; } = string.Empty;
        public string ReturnUrl { get; init; } = string.Empty;
    }

    /// <summary>
    /// Result of creating a billing portal session.
    /// </summary>
    public record CreatePortalResult
    {
        public string PortalUrl { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public PaymentGateway Gateway { get; init; }
    }

    /// <summary>
    /// Normalized customer information across gateways.
    /// </summary>
    public record CustomerInfo
    {
        public string GatewayCustomerId { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public PaymentGateway Gateway { get; init; }
    }

    /// <summary>
    /// Normalized subscription information across gateways.
    /// </summary>
    public record SubscriptionInfo
    {
        public string GatewaySubscriptionId { get; init; } = string.Empty;
        public string GatewayPriceId { get; init; } = string.Empty;
        public string PricingPackageCode { get; init; } = string.Empty;
        public SubscriptionStatus Status { get; init; }
        public DateTime CurrentPeriodStart { get; init; }
        public DateTime CurrentPeriodEnd { get; init; }
        public DateTime? TrialEndsAt { get; init; }
        public DateTime? CancelledAt { get; init; }
        public PaymentGateway Gateway { get; init; }
    }

    /// <summary>
    /// Webhook event normalized across gateways.
    /// </summary>
    public record WebhookEvent
    {
        public string GatewayEventId { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public string PayloadJson { get; init; } = string.Empty;
        public PaymentGateway Gateway { get; init; }
        public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

        public WebhookEvent() { }

        public WebhookEvent(string gatewayEventId, string eventType, string payloadJson, PaymentGateway gateway)
        {
            GatewayEventId = gatewayEventId;
            EventType = eventType;
            PayloadJson = payloadJson;
            Gateway = gateway;
        }
    }
}