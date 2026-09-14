using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.Extensions.Logging;

namespace Identity.Microservice.Services.Implementation.Payments
{
    /// <summary>
    /// Factory for resolving payment gateway implementations.
    /// </summary>
    public class PaymentGatewayFactory : IPaymentGatewayFactory
    {
        private readonly Dictionary<PaymentGateway, IPaymentGateway> _gateways;
        private readonly IPaymentGateway _defaultGateway;
        private readonly ILogger<PaymentGatewayFactory> _logger;

        public PaymentGatewayFactory(
            IEnumerable<IPaymentGateway> gateways,
            ILogger<PaymentGatewayFactory> logger)
        {
            _logger = logger;
            _gateways = gateways.ToDictionary(g => g.GatewayType, g => g);
            
            // Default to PeachPayments if available, otherwise first registered
            _defaultGateway = _gateways.TryGetValue(PaymentGateway.PeachPayments, out var peach)
                ? peach
                : _gateways.Values.FirstOrDefault() 
                    ?? throw new InvalidOperationException("No payment gateways registered");

            _logger.LogInformation("Payment gateway factory initialized with {Count} gateways. Default: {Default}", 
                _gateways.Count, _defaultGateway.GatewayType);
        }

        public IPaymentGateway GetGateway(PaymentGateway gateway)
        {
            if (_gateways.TryGetValue(gateway, out var implementation))
            {
                return implementation;
            }
            
            _logger.LogWarning("Payment gateway {Gateway} not registered, falling back to default", gateway);
            return _defaultGateway;
        }

        public IPaymentGateway GetDefaultGateway()
        {
            return _defaultGateway;
        }

        public IReadOnlyList<IPaymentGateway> GetAllGateways()
        {
            return _gateways.Values.ToList().AsReadOnly();
        }

        public IPaymentGateway GetGatewayForTenant(string tenantId, PaymentGateway? preferred = null)
        {
            // If tenant has a preferred gateway, use it
            if (preferred.HasValue)
            {
                return GetGateway(preferred.Value);
            }

            // Could add tenant-specific gateway preference lookup here
            // For now, return default
            return _defaultGateway;
        }
    }
}