using Identity.Microservice.Services.Abstractions.Payments;

namespace Identity.Microservice.Configuration;

/// <summary>Provider availability and trusted browser return destinations.</summary>
public sealed class PaymentSettings
{
    public PaymentGateway DefaultGateway { get; set; } = PaymentGateway.PayFast;
    public PaymentGateway[] EnabledGateways { get; set; } = [PaymentGateway.PayFast];
    public string[] AllowedReturnOrigins { get; set; } = ["http://localhost:5173"];
}
