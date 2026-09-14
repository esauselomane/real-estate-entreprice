using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Identity.Microservice.Configuration;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Microservice.Services.Implementation.Payments
{
    /// <summary>
    /// Peach Payments gateway implementation using the common payment gateway abstraction.
    /// </summary>
    public class PeachPaymentsGateway : PaymentGatewayBase
    {
        private readonly PeachPaymentsSettings _settings;

        public override PaymentGateway GatewayType => PaymentGateway.PeachPayments;

        public PeachPaymentsGateway(
            HttpClient httpClient,
            IOptions<PeachPaymentsSettings> settings,
            ILogger<PeachPaymentsGateway> logger) : base(httpClient, logger)
        {
            _settings = settings.Value;
            
            HttpClient.BaseAddress = new Uri(_settings.BaseUrl);
            HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiSecret);
            HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public override async Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            var priceId = GetPriceId(request.PricingPackageCode, request.BillingCycle);
            
            var payload = new
            {
                customer = new
                {
                    email = request.CustomerEmail,
                    name = request.CustomerName
                },
                line_items = new[]
                {
                    new
                    {
                        price = priceId,
                        quantity = 1
                    }
                },
                mode = "subscription",
                success_url = request.SuccessUrl,
                cancel_url = request.CancelUrl,
                metadata = new
                {
                    tenant_id = request.TenantId,
                    pricing_package_code = request.PricingPackageCode,
                    billing_cycle = request.BillingCycle.ToString().ToLowerInvariant()
                }
            };

            Logger.LogInformation("Creating Peach checkout session for tenant {TenantId}, package {Package}, cycle {Cycle}", 
                request.TenantId, request.PricingPackageCode, request.BillingCycle);

            var response = await HttpClient.PostAsJsonAsync("/v1/checkout/sessions", payload, JsonOptions, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("Peach checkout session creation failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Peach Payments checkout failed: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PeachCheckoutSessionResponse>(JsonOptions, cancellationToken);
            
            if (result == null || string.IsNullOrEmpty(result.Url))
            {
                throw new InvalidOperationException("Peach Payments returned invalid checkout session response");
            }

            return new CreateCheckoutResult
            {
                CheckoutUrl = result.Url,
                SessionId = result.Id,
                ExpiresAt = result.ExpiresAt,
                Gateway = PaymentGateway.PeachPayments
            };
        }

        public override async Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request, CancellationToken cancellationToken = default)
        {
            // This requires the Peach customer ID which should be stored in our database
            // The actual customer ID resolution happens in the orchestration layer
            var customerId = await GetPeachCustomerIdFromContextAsync(request.TenantId, cancellationToken);
            
            if (string.IsNullOrEmpty(customerId))
            {
                throw new InvalidOperationException($"No Peach customer found for tenant {request.TenantId}");
            }

            var payload = new
            {
                customer = customerId,
                return_url = request.ReturnUrl
            };

            Logger.LogInformation("Creating Peach billing portal session for tenant {TenantId}", request.TenantId);

            var response = await HttpClient.PostAsJsonAsync("/v1/billing_portal/sessions", payload, JsonOptions, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("Peach portal session creation failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Peach Payments portal failed: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PeachPortalSessionResponse>(JsonOptions, cancellationToken);
            
            if (result == null || string.IsNullOrEmpty(result.Url))
            {
                throw new InvalidOperationException("Peach Payments returned invalid portal session response");
            }

            return new CreatePortalResult
            {
                PortalUrl = result.Url,
                SessionId = result.Id,
                Gateway = PaymentGateway.PeachPayments
            };
        }

        public override async Task<CustomerInfo?> GetOrCreateCustomerAsync(string tenantId, string email, string name, CancellationToken cancellationToken = default)
        {
            var existingCustomerId = await GetPeachCustomerIdFromContextAsync(tenantId, cancellationToken);
            
            if (!string.IsNullOrEmpty(existingCustomerId))
            {
                return await GetCustomerAsync(existingCustomerId, cancellationToken);
            }

            var payload = new
            {
                email,
                name,
                metadata = new { tenant_id = tenantId }
            };

            Logger.LogInformation("Creating Peach customer for tenant {TenantId}", tenantId);

            var response = await HttpClient.PostAsJsonAsync("/v1/customers", payload, JsonOptions, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("Peach customer creation failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Peach Payments customer creation failed: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PeachCustomerResponse>(JsonOptions, cancellationToken);
            
            return new CustomerInfo
            {
                GatewayCustomerId = result?.Id ?? string.Empty,
                Email = result?.Email ?? email,
                Name = result?.Name ?? name,
                Gateway = PaymentGateway.PeachPayments
            };
        }

        public override async Task<SubscriptionInfo?> GetSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Fetching Peach subscription {SubscriptionId}", gatewaySubscriptionId);

            var response = await HttpClient.GetAsync($"/v1/subscriptions/{gatewaySubscriptionId}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("Peach subscription fetch failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Peach Payments subscription fetch failed: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PeachSubscriptionResponse>(JsonOptions, cancellationToken);
            
            if (result == null) return null;

            return new SubscriptionInfo
            {
                GatewaySubscriptionId = result.Id,
                GatewayPriceId = result.PriceId,
                PricingPackageCode = ExtractPackageCodeFromPriceId(result.PriceId),
                Status = NormalizeStatus(result.Status, PaymentGateway.PeachPayments),
                CurrentPeriodStart = result.CurrentPeriodStart,
                CurrentPeriodEnd = result.CurrentPeriodEnd,
                TrialEndsAt = result.TrialEnd,
                CancelledAt = result.CancelledAt,
                Gateway = PaymentGateway.PeachPayments
            };
        }

        public override async Task<SubscriptionInfo?> CancelSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Cancelling Peach subscription {SubscriptionId}", gatewaySubscriptionId);

            var response = await HttpClient.DeleteAsync($"/v1/subscriptions/{gatewaySubscriptionId}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                Logger.LogError("Peach subscription cancellation failed: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Peach Payments subscription cancellation failed: {response.StatusCode} - {error}");
            }

            return await GetSubscriptionAsync(gatewaySubscriptionId, cancellationToken);
        }

        public override async Task<bool> ValidateWebhookSignatureAsync(HttpRequest request, string payload, CancellationToken cancellationToken = default)
        {
            var signature = request.Headers["Peach-Signature"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(signature))
            {
                Logger.LogWarning("Webhook request missing Peach-Signature header");
                return false;
            }

            // Parse signature: t=timestamp,v1=signature
            var parts = signature.Split(',')
                .Select(p => p.Split('=', 2))
                .ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : string.Empty);

            if (!parts.TryGetValue("t", out var timestamp) || !parts.TryGetValue("v1", out var expectedSignature))
            {
                Logger.LogWarning("Invalid Peach-Signature format: {Signature}", signature);
                return false;
            }

            var signedPayload = $"{timestamp}.{payload}";
            var computedSignature = ComputeHmacSha256(_settings.WebhookSecret, signedPayload);

            var isValid = ConstantTimeEquals(expectedSignature, computedSignature);

            if (!isValid)
            {
                Logger.LogWarning("Webhook signature validation failed for event");
            }

            return isValid;
        }

        public override WebhookEvent ParseWebhookEvent(string payload)
        {
            var webhookData = JsonDocument.Parse(payload).RootElement;
            
            return new WebhookEvent
            {
                GatewayEventId = GetStringProperty(webhookData, "id") ?? Guid.NewGuid().ToString(),
                EventType = GetStringProperty(webhookData, "type") ?? "unknown",
                PayloadJson = payload,
                Gateway = PaymentGateway.PeachPayments
            };
        }

        public override string GetPriceId(string pricingPackageCode, BillingCycle billingCycle)
        {
            var priceMap = new Dictionary<string, Dictionary<BillingCycle, string>>
            {
                ["SME_AGENCY"] = new()
                {
                    [BillingCycle.Monthly] = "price_sme_monthly",
                    [BillingCycle.Annual] = "price_sme_annual"
                },
                ["PRIVATE"] = new()
                {
                    [BillingCycle.Monthly] = "price_private_monthly",
                    [BillingCycle.Annual] = "price_private_annual"
                },
                ["ENTERPRISE"] = new()
                {
                    [BillingCycle.Monthly] = "price_enterprise_monthly",
                    [BillingCycle.Annual] = "price_enterprise_annual"
                }
            };

            if (priceMap.TryGetValue(pricingPackageCode, out var cycleMap) && 
                cycleMap.TryGetValue(billingCycle, out var priceId))
            {
                return priceId;
            }

            throw new ArgumentException($"No price ID configured for package {pricingPackageCode} with billing cycle {billingCycle}");
        }

        // These methods need to be implemented by the orchestration layer
        // They are virtual so they can be overridden or we can inject a resolver
        protected virtual Task<string?> GetPeachCustomerIdFromContextAsync(string tenantId, CancellationToken cancellationToken)
        {
            // This should be overridden by a decorator or resolved via DI
            // For now, return null - the orchestration service will handle this
            return Task.FromResult<string?>(null);
        }

        protected virtual Task<CustomerInfo?> GetCustomerAsync(string customerId, CancellationToken cancellationToken)
        {
            var response = HttpClient.GetAsync($"/v1/customers/{customerId}", cancellationToken).Result;
            
            if (!response.IsSuccessStatusCode)
            {
                return Task.FromResult<CustomerInfo?>(null);
            }

            var result = response.Content.ReadFromJsonAsync<PeachCustomerResponse>(JsonOptions, cancellationToken).Result;
            
            if (result == null) return Task.FromResult<CustomerInfo?>(null);
            
            return Task.FromResult<CustomerInfo?>(new CustomerInfo
            {
                GatewayCustomerId = result.Id,
                Email = result.Email,
                Name = result.Name,
                Gateway = PaymentGateway.PeachPayments
            });
        }

        private static string ExtractPackageCodeFromPriceId(string priceId)
        {
            if (priceId.StartsWith("price_sme_")) return "SME_AGENCY";
            if (priceId.StartsWith("price_private_")) return "PRIVATE";
            if (priceId.StartsWith("price_enterprise_")) return "ENTERPRISE";
            return "UNKNOWN";
        }
    }
}

// Peach API response DTOs
public record PeachCheckoutSessionResponse
{
    public string Id { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public record PeachPortalSessionResponse
{
    public string Id { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

public record PeachCustomerResponse
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public record PeachSubscriptionResponse
{
    public string Id { get; init; } = string.Empty;
    public string PriceId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CurrentPeriodStart { get; init; }
    public DateTime CurrentPeriodEnd { get; init; }
    public DateTime? TrialEnd { get; init; }
    public DateTime? CancelledAt { get; init; }
}