using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions.Payments;

namespace Identity.Microservice.DTOs;

/// <summary>Audited support correction; does not charge, refund or cancel a provider agreement.</summary>
public sealed record BillingOverrideRequest
{
    public string TenantId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int SubscriptionId { get; init; }
    public SubscriptionStatus Status { get; init; }
    public DateTime CurrentPeriodEnd { get; init; }
    public PaymentGateway? Gateway { get; init; }
    public string? GatewaySubscriptionId { get; init; }
}
