using Identity.Microservice.Services.Abstractions.Payments;

namespace Identity.Microservice.Configuration;

/// <summary>Provider availability and trusted browser return destinations.</summary>
public sealed class PaymentSettings
{
    /// <summary>Explicit tenant allowed a single R2 initial PayFast payment for Mothodi card testing.</summary>
    public string? MothodiTestTenantId { get; set; }

    public PaymentGateway DefaultGateway { get; set; } = PaymentGateway.PayFast;
    public PaymentGateway[] EnabledGateways { get; set; } = [PaymentGateway.PayFast];
    public string[] AllowedReturnOrigins { get; set; } = ["http://localhost:5173"];
}
