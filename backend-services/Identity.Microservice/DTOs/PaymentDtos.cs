namespace Identity.Microservice.DTOs
{
    public record CreateCheckoutSessionRequest
    {
        public string PricingPackageCode { get; init; } = string.Empty;
        public string BillingCycle { get; init; } = "monthly";
        public string SuccessUrl { get; init; } = string.Empty;
        public string CancelUrl { get; init; } = string.Empty;
    }

    public record CreateCheckoutSessionResponse
    {
        public string CheckoutUrl { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
    }

    public record CreatePortalSessionRequest
    {
        public string ReturnUrl { get; init; } = string.Empty;
    }

    public record CreatePortalSessionResponse
    {
        public string PortalUrl { get; init; } = string.Empty;
        public string SessionId { get; init; } = string.Empty;
    }

    public record SubscriptionStatusResponse
    {
        public string Status { get; init; } = string.Empty;
        public string PricingPackageCode { get; init; } = string.Empty;
        public string PricingPackageName { get; init; } = string.Empty;
        public decimal? MonthlyPrice { get; init; }
        public decimal? AnnualPrice { get; init; }
        public DateTime CurrentPeriodStart { get; init; }
        public DateTime CurrentPeriodEnd { get; init; }
        public DateTime? TrialEndsAt { get; init; }
        public int DaysUntilExpiry { get; init; }
        public bool CanUpgrade { get; init; }
        public bool IsTrial { get; init; }
    }

    public record WebhookEventDto
    {
        public int Id { get; init; }
        public string PeachEventId { get; init; } = string.Empty;
        public string EventType { get; init; } = string.Empty;
        public bool Processed { get; init; }
        public DateTime ReceivedAt { get; init; }
        public DateTime? ProcessedAt { get; init; }
        public string? ErrorMessage { get; init; }
    }
}