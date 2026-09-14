using Identity.Microservice.Services.Abstractions.Payments;

namespace Identity.Microservice.Services.Abstractions.Payments
{
    /// <summary>
    /// Factory for resolving payment gateway implementations.
    /// Allows runtime selection of gateway based on configuration or tenant preference.
    /// </summary>
    public interface IPaymentGatewayFactory
    {
        /// <summary>
        /// Get a specific payment gateway by type.
        /// </summary>
        IPaymentGateway GetGateway(PaymentGateway gateway);

        /// <summary>
        /// Get the default/primary payment gateway.
        /// </summary>
        IPaymentGateway GetDefaultGateway();

        /// <summary>
        /// Get all registered gateways.
        /// </summary>
        IReadOnlyList<IPaymentGateway> GetAllGateways();

        /// <summary>
        /// Get the preferred gateway for a tenant (falls back to default).
        /// </summary>
        IPaymentGateway GetGatewayForTenant(string tenantId, PaymentGateway? preferred = null);
    }
}