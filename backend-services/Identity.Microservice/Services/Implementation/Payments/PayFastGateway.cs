using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Identity.Microservice.Configuration;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Microservice.Services.Implementation.Payments
{
    /// <summary>
    /// PayFast payment gateway implementation.
    /// Supports subscriptions via PayFast Recurring Billing.
    /// </summary>
    public class PayFastGateway : PaymentGatewayBase
    {
        private readonly PayFastSettings _settings;

        public override PaymentGateway GatewayType => PaymentGateway.PayFast;

        public PayFastGateway(
            HttpClient httpClient,
            IOptions<PayFastSettings> settings,
            ILogger<PayFastGateway> logger) : base(httpClient, logger)
        {
            _settings = settings.Value;
            
            HttpClient.BaseAddress = new Uri(_settings.BaseUrl);
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public override async Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            var priceId = GetPriceId(request.PricingPackageCode, request.BillingCycle);
            var amount = GetAmountForPrice(priceId, request.BillingCycle);

            // PayFast uses a different flow - generate payment form data
            var paymentData = new Dictionary<string, string>
            {
                ["merchant_id"] = _settings.MerchantId,
                ["merchant_key"] = _settings.MerchantKey,
                ["return_url"] = request.SuccessUrl,
                ["cancel_url"] = request.CancelUrl,
                ["notify_url"] = $"{_settings.BaseUrl}/api/payments/webhook/payfast",
                ["name_first"] = request.CustomerName?.Split(' ')[0] ?? "Customer",
                ["name_last"] = request.CustomerName?.Split(' ').Length > 1 ? request.CustomerName.Split(' ')[1] : "User",
                ["email_address"] = request.CustomerEmail ?? "",
                ["m_payment_id"] = $"sub_{request.TenantId}_{request.PricingPackageCode}_{request.BillingCycle}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                ["amount"] = amount.ToString("F2"),
                ["currency"] = "ZAR",
                ["item_name"] = $"{request.PricingPackageCode} Subscription ({request.BillingCycle})",
                ["item_description"] = $"Real Estate Enterprise - {request.PricingPackageCode} Plan",
                ["recurring"] = "1",
                ["frequency"] = request.BillingCycle == BillingCycle.Monthly ? "3" : "12", // 3=monthly, 12=yearly
                ["cycles"] = "0" // 0 = unlimited
            };

            // Generate signature
            var signature = GenerateSignature(paymentData);
            paymentData["signature"] = signature;

            // Build the checkout URL with form data
            var queryString = string.Join("&", paymentData.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            var checkoutUrl = $"{_settings.ProcessUrl}?{queryString}";

            Logger.LogInformation("Created PayFast checkout for tenant {TenantId}, package {Package}, cycle {Cycle}", 
                request.TenantId, request.PricingPackageCode, request.BillingCycle);

            return new CreateCheckoutResult
            {
                CheckoutUrl = checkoutUrl,
                SessionId = paymentData["m_payment_id"],
                ExpiresAt = DateTime.UtcNow.AddHours(24), // PayFast doesn't have explicit expiry
                Gateway = PaymentGateway.PayFast
            };
        }

        public override async Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request, CancellationToken cancellationToken = default)
        {
            // PayFast doesn't have a hosted billing portal like Peach/Stripe
            // Redirect to PayFast subscriber management or our own portal
            var portalUrl = $"{_settings.BaseUrl}/subscriber/manage";
            
            return new CreatePortalResult
            {
                PortalUrl = portalUrl,
                SessionId = $"portal_{request.TenantId}_{DateTime.UtcNow:yyyyMMddHHmmss}",
                Gateway = PaymentGateway.PayFast
            };
        }

        public override async Task<CustomerInfo?> GetOrCreateCustomerAsync(string tenantId, string email, string name, CancellationToken cancellationToken = default)
        {
            // PayFast doesn't have a customer vault API in the same way
            // Customers are identified by email in recurring billing
            return new CustomerInfo
            {
                GatewayCustomerId = email, // Use email as identifier
                Email = email,
                Name = name,
                Gateway = PaymentGateway.PayFast
            };
        }

        public override async Task<SubscriptionInfo?> GetSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default)
        {
            // PayFast subscription query via API
            var url = $"{_settings.SubscriptionsUrl}/{gatewaySubscriptionId}";
            
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.MerchantId}:{_settings.MerchantKey}")));

            var response = await HttpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                    
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("PayFast subscription fetch failed: {StatusCode} - {Error}", response.StatusCode, error);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonDocument.Parse(json).RootElement;

            return new SubscriptionInfo
            {
                GatewaySubscriptionId = gatewaySubscriptionId,
                GatewayPriceId = GetStringProperty(data, "amount", "0"),
                PricingPackageCode = GetStringProperty(data, "item_name", "UNKNOWN").Split(' ')[0],
                Status = NormalizeStatus(GetStringProperty(data, "status"), PaymentGateway.PayFast),
                CurrentPeriodStart = GetNullableDateTimeProperty(data, "created_at") ?? DateTime.UtcNow,
                CurrentPeriodEnd = GetNullableDateTimeProperty(data, "next_payment_date") ?? DateTime.UtcNow.AddMonths(1),
                TrialEndsAt = null,
                CancelledAt = GetNullableDateTimeProperty(data, "cancelled_at"),
                Gateway = PaymentGateway.PayFast
            };
        }

        public override async Task<SubscriptionInfo?> CancelSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default)
        {
            var url = $"{_settings.SubscriptionsUrl}/{gatewaySubscriptionId}/cancel";
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", 
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.MerchantId}:{_settings.MerchantKey}")));

            var response = await HttpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("PayFast subscription cancellation failed: {StatusCode} - {Error}", response.StatusCode, error);
                return null;
            }

            return await GetSubscriptionAsync(gatewaySubscriptionId, cancellationToken);
        }

        public override async Task<bool> ValidateWebhookSignatureAsync(HttpRequest request, string payload, CancellationToken cancellationToken = default)
        {
            // PayFast uses a different signature validation
            // They send a signature parameter in the form data
            var form = await request.ReadFormAsync(cancellationToken);
            var receivedSignature = form["signature"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(receivedSignature))
            {
                Logger.LogWarning("PayFast webhook missing signature");
                return false;
            }

            // Remove signature from form data for validation
            var formData = form.Where(kvp => kvp.Key != "signature")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
            
            var expectedSignature = GenerateSignature(formData);
            
            return ConstantTimeEquals(receivedSignature, expectedSignature);
        }

        public override WebhookEvent ParseWebhookEvent(string payload)
        {
            // PayFast sends form-encoded data, not JSON
            var formData = System.Web.HttpUtility.ParseQueryString(payload);
            var eventType = DetermineEventType(formData);
            
            return new WebhookEvent
            {
                GatewayEventId = formData["pf_payment_id"] ?? Guid.NewGuid().ToString(),
                EventType = eventType,
                PayloadJson = JsonSerializer.Serialize(formData.AllKeys.ToDictionary(k => k, k => formData[k])),
                Gateway = PaymentGateway.PayFast
            };
        }

        public override string GetPriceId(string pricingPackageCode, BillingCycle billingCycle)
        {
            // PayFast uses amount-based pricing, not price IDs
            // Return a composite key
            return $"{pricingPackageCode}_{billingCycle}";
        }

        private string GenerateSignature(Dictionary<string, string> data)
        {
            var sortedKeys = data.Keys.OrderBy(k => k).ToList();
            var paramString = string.Join("&", sortedKeys.Select(k => $"{k}={Uri.EscapeDataString(data[k])}"));
            
            if (!string.IsNullOrEmpty(_settings.PassPhrase))
            {
                paramString += $"&passphrase={Uri.EscapeDataString(_settings.PassPhrase)}";
            }

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(paramString));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private decimal GetAmountForPrice(string priceId, BillingCycle billingCycle)
        {
            // Parse pricing from priceId (format: PACKAGE_CYCLE)
            var parts = priceId.Split('_');
            if (parts.Length < 2) return 299; // Default
            
            var packageCode = parts[0];
            var isAnnual = parts[1].Equals("Annual", StringComparison.OrdinalIgnoreCase) || billingCycle == BillingCycle.Annual;

            return (packageCode, isAnnual) switch
            {
                ("SME_AGENCY", false) => 299.00m,
                ("SME_AGENCY", true) => 2990.00m,
                ("PRIVATE", false) => 799.00m,
                ("PRIVATE", true) => 7990.00m,
                ("ENTERPRISE", false) => 1999.00m,
                ("ENTERPRISE", true) => 19990.00m,
                _ => 299.00m
            };
        }

        private static string DetermineEventType(NameValueCollection formData)
        {
            var status = formData["subscription_status"] ?? formData["payment_status"];
            
            return status?.ToLowerInvariant() switch
            {
                "active" => "subscription.activated",
                "pending" => "subscription.pending",
                "cancelled" => "subscription.cancelled",
                "expired" => "subscription.expired",
                "suspended" => "subscription.suspended",
                "complete" => "payment.completed",
                "failed" => "payment.failed",
                _ => "unknown"
            };
        }
    }
}