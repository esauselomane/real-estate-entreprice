using Identity.Microservice.Configuration;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.Extensions.Options;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Resolves explicitly enabled providers without silently switching ownership.</summary>
public sealed class PaymentGatewayFactory : IPaymentGatewayFactory
{
    private readonly Dictionary<PaymentGateway, IPaymentGateway> gateways;
    private readonly PaymentGateway defaultGateway;

    public PaymentGatewayFactory(IEnumerable<IPaymentGateway> providers, IOptions<PaymentSettings> options)
    {
        var settings = options.Value;
        gateways = providers.Where(provider => settings.EnabledGateways.Contains(provider.GatewayType))
            .ToDictionary(provider => provider.GatewayType);
        defaultGateway = settings.DefaultGateway;
    }

    public IPaymentGateway GetGateway(PaymentGateway gateway)
    {
        if (!gateways.TryGetValue(gateway, out var provider))
            throw new InvalidOperationException($"Payment provider {gateway} is not enabled. Contact billing support.");
        return provider;
    }

    public IPaymentGateway GetDefaultGateway() => GetGateway(defaultGateway);
    public IReadOnlyList<IPaymentGateway> GetAllGateways() => gateways.Values.ToList().AsReadOnly();
    public IPaymentGateway GetGatewayForTenant(string tenantId, PaymentGateway? preferred = null) =>
        GetGateway(preferred ?? defaultGateway);
}
