using Identity.Microservice.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Microservice.Services.Abstractions.Payments
{
    /// <summary>
    /// Base class for payment gateway implementations providing common HTTP and security utilities.
    /// </summary>
    public abstract class PaymentGatewayBase : IPaymentGateway
    {
        protected readonly HttpClient HttpClient;
        protected readonly ILogger Logger;
        protected readonly JsonSerializerOptions JsonOptions;

        protected PaymentGatewayBase(HttpClient httpClient, ILogger logger)
        {
            HttpClient = httpClient;
            Logger = logger;
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public abstract PaymentGateway GatewayType { get; }
        public abstract bool SupportsPortal { get; }

        public abstract Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default);

        public abstract Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request, CancellationToken cancellationToken = default);

        public abstract Task<CustomerInfo?> GetOrCreateCustomerAsync(string tenantId, string email, string name, CancellationToken cancellationToken = default);

        public abstract Task<SubscriptionInfo?> GetSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default);

        public abstract Task<SubscriptionInfo?> CancelSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default);

        public abstract Task<bool> ValidateWebhookSignatureAsync(HttpRequest request, string payload, CancellationToken cancellationToken = default);

        public abstract WebhookEvent ParseWebhookEvent(string payload);

        public abstract string GetPriceId(string pricingPackageCode, BillingCycle billingCycle);

        /// <summary>
        /// Compute HMAC-SHA256 signature for webhook validation.
        /// </summary>
        protected static string ComputeHmacSha256(string secret, string payload)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Constant-time comparison for signature validation.
        /// </summary>
        protected static bool ConstantTimeEquals(string a, string b)
        {
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a),
                Encoding.UTF8.GetBytes(b));
        }

        /// <summary>
        /// Normalize gateway-specific status to common SubscriptionStatus.
        /// </summary>
        protected static SubscriptionStatus NormalizeStatus(string? gatewayStatus, PaymentGateway gateway)
        {
            return gateway switch
            {
                PaymentGateway.PeachPayments => NormalizePeachStatus(gatewayStatus),
                PaymentGateway.PayFast => NormalizePayFastStatus(gatewayStatus),
                _ => SubscriptionStatus.Trialing
            };
        }

        private static SubscriptionStatus NormalizePeachStatus(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "trialing" => SubscriptionStatus.Trialing,
                "active" => SubscriptionStatus.Active,
                "past_due" => SubscriptionStatus.PastDue,
                "canceled" or "cancelled" => SubscriptionStatus.Cancelled,
                "expired" => SubscriptionStatus.Expired,
                "paused" => SubscriptionStatus.Paused,
                _ => SubscriptionStatus.Trialing
            };
        }

        private static SubscriptionStatus NormalizePayFastStatus(string? status)
        {
            return status?.ToLowerInvariant() switch
            {
                "active" => SubscriptionStatus.Active,
                "pending" => SubscriptionStatus.Trialing,
                "cancelled" => SubscriptionStatus.Cancelled,
                "expired" => SubscriptionStatus.Expired,
                "suspended" => SubscriptionStatus.Paused,
                _ => SubscriptionStatus.Trialing
            };
        }

        /// <summary>
        /// Convert Unix timestamp to DateTime.
        /// </summary>
        protected static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeStamp).UtcDateTime;
        }

        /// <summary>
        /// Safely get string property from JsonElement.
        /// </summary>
        protected static string? GetStringProperty(JsonElement element, string propertyName, string? defaultValue = null)
        {
            if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
                return defaultValue;

            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
                return prop.GetString();

            return defaultValue;
        }

        /// <summary>
        /// Safely get DateTime property from JsonElement (Unix timestamp or ISO string).
        /// </summary>
        protected static DateTime GetDateTimeProperty(JsonElement element, string propertyName, DateTime defaultValue = default)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return UnixTimeStampToDateTime(prop.GetInt64());
                if (prop.ValueKind == JsonValueKind.String && DateTime.TryParse(prop.GetString(), out var dt))
                    return dt;
            }
            return defaultValue == default ? DateTime.UtcNow : defaultValue;
        }

        /// <summary>
        /// Safely get nullable DateTime property from JsonElement.
        /// </summary>
        protected static DateTime? GetNullableDateTimeProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
            {
                if (prop.ValueKind == JsonValueKind.Number)
                    return UnixTimeStampToDateTime(prop.GetInt64());
                if (prop.ValueKind == JsonValueKind.String && DateTime.TryParse(prop.GetString(), out var dt))
                    return dt;
            }
            return null;
        }
    }
}