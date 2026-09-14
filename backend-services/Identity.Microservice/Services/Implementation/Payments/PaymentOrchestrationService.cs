using System.Text.Json;
using System.Text.Json.Nodes;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelWebhookEvent = Identity.Microservice.Models.WebhookEvent;
using PaymentWebhookEvent = Identity.Microservice.Services.Abstractions.Payments.WebhookEvent;
using ModelSubscriptionStatus = Identity.Microservice.Models.SubscriptionStatus;
using PaymentSubscriptionStatus = Identity.Microservice.Services.Abstractions.Payments.SubscriptionStatus;

namespace Identity.Microservice.Services.Implementation.Payments
{
    /// <summary>
    /// Orchestration service for payment operations.
    /// Handles the business logic that coordinates between gateways, repositories, and domain models.
    /// </summary>
    public class PaymentOrchestrationService
    {
        private readonly IPaymentGatewayFactory _gatewayFactory;
        private readonly IPeachCustomerRepository _peachCustomerRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly IPaymentIntentRepository _paymentIntentRepository;
        private readonly IWebhookEventRepository _webhookEventRepository;
        private readonly IPricingPackageRepository _pricingPackageRepository;
        private readonly ITenantRepository _tenantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<PaymentOrchestrationService> _logger;

        public PaymentOrchestrationService(
            IPaymentGatewayFactory gatewayFactory,
            IPeachCustomerRepository peachCustomerRepository,
            ISubscriptionRepository subscriptionRepository,
            IPaymentIntentRepository paymentIntentRepository,
            IWebhookEventRepository webhookEventRepository,
            IPricingPackageRepository pricingPackageRepository,
            ITenantRepository tenantRepository,
            IUserRepository userRepository,
            IEmailService emailService,
            ILogger<PaymentOrchestrationService> logger)
        {
            _gatewayFactory = gatewayFactory;
            _peachCustomerRepository = peachCustomerRepository;
            _subscriptionRepository = subscriptionRepository;
            _paymentIntentRepository = paymentIntentRepository;
            _webhookEventRepository = webhookEventRepository;
            _pricingPackageRepository = pricingPackageRepository;
            _tenantRepository = tenantRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _logger = logger;
        }

        /// <summary>
        /// Create a checkout session for subscription signup.
        /// </summary>
        public async Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request)
        {
            // Validate pricing package
            var pricingPackage = await _pricingPackageRepository.GetByCodeAsync(request.PricingPackageCode);
            if (pricingPackage == null || !pricingPackage.IsActive)
            {
                throw new ArgumentException("Invalid or inactive pricing package");
            }

            // Get tenant info
            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId);
            if (tenant == null)
            {
                throw new ArgumentException("Tenant not found");
            }

            // Determine gateway
            var gateway = _gatewayFactory.GetGatewayForTenant(request.TenantId, request.PreferredGateway);
            
            // Get or create customer in the gateway
            var customer = await GetOrCreateGatewayCustomerAsync(gateway, request.TenantId, tenant.ContactEmail, tenant.AgencyName);
            
            // Generate idempotency key
            var idempotencyKey = $"checkout_{request.TenantId}_{request.PricingPackageCode}_{request.BillingCycle}_{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Check for existing pending payment intent
            var existingIntent = await _paymentIntentRepository.GetByIdempotencyKeyAsync(idempotencyKey);
            if (existingIntent != null && existingIntent.Status == PaymentIntentStatus.Pending)
            {
                _logger.LogInformation("Returning existing checkout session for idempotency key {Key}", idempotencyKey);
            }

            // Create payment intent record
            var amount = request.BillingCycle == BillingCycle.Annual ? pricingPackage.AnnualPrice ?? 0 : pricingPackage.MonthlyPrice ?? 0;
            var paymentIntent = new PaymentIntent
            {
                TenantId = request.TenantId,
                PeachCheckoutId = "", // Will be filled after session creation
                Amount = amount,
                Currency = "ZAR",
                Status = PaymentIntentStatus.Pending,
                IdempotencyKey = idempotencyKey,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    pricingPackageCode = request.PricingPackageCode,
                    billingCycle = request.BillingCycle.ToString(),
                    gateway = gateway.GatewayType.ToString()
                })
            };
            _paymentIntentRepository.Add(paymentIntent);

            try
            {
                var gatewayRequest = new CreateCheckoutRequest
                {
                    TenantId = request.TenantId,
                    PricingPackageCode = request.PricingPackageCode,
                    BillingCycle = request.BillingCycle,
                    SuccessUrl = request.SuccessUrl,
                    CancelUrl = request.CancelUrl,
                    CustomerEmail = tenant.ContactEmail,
                    CustomerName = tenant.AgencyName,
                    PreferredGateway = gateway.GatewayType
                };

                var checkoutResult = await gateway.CreateCheckoutSessionAsync(gatewayRequest);

                // Update payment intent with session ID
                paymentIntent.PeachCheckoutId = checkoutResult.SessionId;
                paymentIntent.Status = PaymentIntentStatus.Processing;
                paymentIntent.MetadataJson = JsonSerializer.Serialize(new
                {
                    pricingPackageCode = request.PricingPackageCode,
                    billingCycle = request.BillingCycle.ToString(),
                    gateway = checkoutResult.Gateway.ToString(),
                    gatewaySessionId = checkoutResult.SessionId
                });

                _logger.LogInformation("Created checkout session via {Gateway} for tenant {TenantId}", 
                    checkoutResult.Gateway, request.TenantId);

                return checkoutResult;
            }
            catch (Exception ex)
            {
                paymentIntent.Status = PaymentIntentStatus.Failed;
                _logger.LogError(ex, "Failed to create checkout session for tenant {TenantId} via {Gateway}", 
                    request.TenantId, gateway.GatewayType);
                throw;
            }
        }

        /// <summary>
        /// Create a billing portal session for subscription management.
        /// </summary>
        public async Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request)
        {
            var tenant = await _tenantRepository.GetByIdAsync(request.TenantId);
            if (tenant == null)
            {
                throw new ArgumentException("Tenant not found");
            }

            var gateway = _gatewayFactory.GetGatewayForTenant(request.TenantId);
            
            return await gateway.CreatePortalSessionAsync(request);
        }

        /// <summary>
        /// Get subscription status for a tenant.
        /// </summary>
        public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(string tenantId)
        {
            var subscription = await _subscriptionRepository.GetActiveByTenantIdAsync(tenantId);
            var tenant = await _tenantRepository.GetByIdAsync(tenantId);

            if (subscription == null)
            {
                var isTrial = tenant?.IsTrial == true && tenant.TrialEndDate.HasValue;
                var trialEndsAt = tenant?.TrialEndDate;
                var daysLeft = trialEndsAt.HasValue ? (int)Math.Ceiling((trialEndsAt.Value - DateTime.UtcNow).TotalDays) : 0;

                return new SubscriptionStatusResponse
                {
                    Status = isTrial ? "trialing" : "none",
                    PricingPackageCode = "NONE",
                    PricingPackageName = "No Active Subscription",
                    CurrentPeriodStart = DateTime.UtcNow,
                    CurrentPeriodEnd = trialEndsAt ?? DateTime.UtcNow,
                    TrialEndsAt = trialEndsAt,
                    DaysUntilExpiry = Math.Max(0, daysLeft),
                    CanUpgrade = true,
                    IsTrial = isTrial,
                    Gateway = null
                };
            }

            var pricingPackage = await _pricingPackageRepository.GetByCodeAsync(subscription.PricingPackageCode);
            var daysUntilExpiry = (int)Math.Ceiling((subscription.CurrentPeriodEnd - DateTime.UtcNow).TotalDays);

            return new SubscriptionStatusResponse
            {
                Status = subscription.Status.ToString().ToLowerInvariant(),
                PricingPackageCode = subscription.PricingPackageCode,
                PricingPackageName = pricingPackage?.Name ?? subscription.PricingPackageCode,
                MonthlyPrice = pricingPackage?.MonthlyPrice,
                AnnualPrice = pricingPackage?.AnnualPrice,
                CurrentPeriodStart = subscription.CurrentPeriodStart,
                CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                TrialEndsAt = subscription.TrialEndsAt,
                DaysUntilExpiry = Math.Max(0, daysUntilExpiry),
                CanUpgrade = subscription.Status != ModelSubscriptionStatus.Cancelled && subscription.Status != ModelSubscriptionStatus.Expired,
                IsTrial = subscription.Status == ModelSubscriptionStatus.Trialing,
                Gateway = GetGatewayFromSubscription(subscription)
            };
        }

        public async Task<List<SubscriptionStatusResponse>> GetSubscriptionHistoryAsync(string tenantId)
        {
            var subscriptions = await _subscriptionRepository.Query()
                .Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            if (subscriptions.Count == 0)
            {
                return new List<SubscriptionStatusResponse>();
            }

            var responses = new List<SubscriptionStatusResponse>();
            foreach (var subscription in subscriptions)
            {
                var pricingPackage = await _pricingPackageRepository.GetByCodeAsync(subscription.PricingPackageCode);
                var currentPeriodEnd = subscription.CurrentPeriodEnd == default ? DateTime.UtcNow.AddDays(30) : subscription.CurrentPeriodEnd;

                responses.Add(new SubscriptionStatusResponse
                {
                    Status = subscription.Status.ToString().ToLowerInvariant(),
                    PricingPackageCode = subscription.PricingPackageCode,
                    PricingPackageName = pricingPackage?.Name ?? subscription.PricingPackageCode,
                    MonthlyPrice = pricingPackage?.MonthlyPrice,
                    AnnualPrice = pricingPackage?.AnnualPrice,
                    CurrentPeriodStart = subscription.CurrentPeriodStart,
                    CurrentPeriodEnd = currentPeriodEnd,
                    TrialEndsAt = subscription.TrialEndsAt,
                    DaysUntilExpiry = Math.Max(0, (int)Math.Ceiling((currentPeriodEnd - DateTime.UtcNow).TotalDays)),
                    CanUpgrade = subscription.Status != ModelSubscriptionStatus.Cancelled && subscription.Status != ModelSubscriptionStatus.Expired,
                    IsTrial = subscription.Status == ModelSubscriptionStatus.Trialing,
                    Gateway = GetGatewayFromSubscription(subscription)
                });
            }

            return responses;
        }

        /// <summary>
        /// Process a webhook event from any gateway.
        /// </summary>
        public async Task ProcessWebhookEventAsync(PaymentWebhookEvent webhookEvent, string payload)
        {
            var gateway = _gatewayFactory.GetGateway(webhookEvent.Gateway);
            
            // Idempotency check
            var existingEvent = await _webhookEventRepository.GetUnprocessedByPeachEventIdAsync(webhookEvent.GatewayEventId);
            if (existingEvent == null)
            {
                var processedEvent = await _webhookEventRepository.GetByPeachEventIdAsync(webhookEvent.GatewayEventId);
                if (processedEvent?.Processed == true)
                {
                    _logger.LogInformation("Webhook event {EventId} already processed, skipping", webhookEvent.GatewayEventId);
                    return;
                }
            }

            // Store webhook event
            var webhookRecord = new ModelWebhookEvent
            {
                PeachEventId = webhookEvent.GatewayEventId,
                EventType = webhookEvent.EventType,
                PayloadJson = payload,
                Processed = false
            };
            _webhookEventRepository.Add(webhookRecord);

            try
            {
                await ProcessWebhookEventInternalAsync(gateway, webhookEvent, payload);
                await _webhookEventRepository.MarkProcessedAsync(webhookEvent.GatewayEventId);
                _logger.LogInformation("Successfully processed webhook event {EventId} of type {EventType} from {Gateway}", 
                    webhookEvent.GatewayEventId, webhookEvent.EventType, webhookEvent.Gateway);
            }
            catch (Exception ex)
            {
                await _webhookEventRepository.MarkFailedAsync(webhookEvent.GatewayEventId, ex.Message);
                _logger.LogError(ex, "Failed to process webhook event {EventId} of type {EventType} from {Gateway}", 
                    webhookEvent.GatewayEventId, webhookEvent.EventType, webhookEvent.Gateway);
                throw;
            }
        }

        private async Task ProcessWebhookEventInternalAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            switch (webhookEvent.EventType)
            {
                case "checkout.session.completed":
                case "subscription.activated":
                    await HandleCheckoutCompletedAsync(gateway, webhookEvent, payload);
                    break;
                case "subscription.created":
                    await HandleSubscriptionCreatedAsync(gateway, webhookEvent, payload);
                    break;
                case "subscription.updated":
                    await HandleSubscriptionUpdatedAsync(gateway, webhookEvent, payload);
                    break;
                case "subscription.cancelled":
                    await HandleSubscriptionCancelledAsync(gateway, webhookEvent, payload);
                    break;
                case "subscription.past_due":
                case "subscription.suspended":
                    await HandleSubscriptionPastDueAsync(gateway, webhookEvent, payload);
                    break;
                case "invoice.payment_succeeded":
                case "payment.completed":
                    await HandleInvoicePaymentSucceededAsync(gateway, webhookEvent, payload);
                    break;
                case "invoice.payment_failed":
                case "payment.failed":
                    await HandleInvoicePaymentFailedAsync(gateway, webhookEvent, payload);
                    break;
                case "customer.subscription.trial_will_end":
                    await HandleTrialWillEndAsync(gateway, webhookEvent, payload);
                    break;
                default:
                    _logger.LogInformation("Unhandled webhook event type: {EventType} from {Gateway}", webhookEvent.EventType, gateway.GatewayType);
                    break;
            }
        }

        private async Task HandleCheckoutCompletedAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            // Parse gateway-specific data
            var subscriptionInfo = gateway.ParseWebhookEvent(payload);
            // This would need gateway-specific parsing logic
            // For now, we'll use a simplified approach
            
            // The actual implementation would extract tenant_id, subscription_id, etc. from the payload
            // based on the gateway type
            _logger.LogInformation("Checkout completed for gateway {Gateway}", gateway.GatewayType);
        }

        private async Task HandleSubscriptionCreatedAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var gatewaySubscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            if (string.IsNullOrWhiteSpace(gatewaySubscriptionId))
            {
                _logger.LogWarning("Webhook payload from {Gateway} did not contain a subscription id", gateway.GatewayType);
                return;
            }

            var tenantId = await ResolveTenantIdForSubscriptionAsync(gateway, payload, gatewaySubscriptionId);
            var subscription = await gateway.GetSubscriptionAsync(gatewaySubscriptionId);
            if (subscription != null)
            {
                await UpsertSubscriptionAsync(subscription, tenantId);
            }
        }

        private async Task HandleSubscriptionUpdatedAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var gatewaySubscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            if (string.IsNullOrWhiteSpace(gatewaySubscriptionId))
            {
                _logger.LogWarning("Webhook payload from {Gateway} did not contain a subscription id", gateway.GatewayType);
                return;
            }

            var tenantId = await ResolveTenantIdForSubscriptionAsync(gateway, payload, gatewaySubscriptionId);
            var subscription = await gateway.GetSubscriptionAsync(gatewaySubscriptionId);
            if (subscription != null)
            {
                await UpsertSubscriptionAsync(subscription, tenantId);
            }
        }

        private async Task HandleSubscriptionCancelledAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var subscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(subscriptionId);
            
            if (existingSub != null)
            {
                existingSub.Status = ModelSubscriptionStatus.Cancelled;
                existingSub.CancelledAt = DateTime.UtcNow;
                existingSub.UpdatedAt = DateTime.UtcNow;
                _subscriptionRepository.Update(existingSub);

                var tenant = await _tenantRepository.GetByIdAsync(existingSub.TenantId);
                if (tenant != null)
                {
                    tenant.SubscriptionStatus = ModelSubscriptionStatus.Cancelled;
                    tenant.OperationalStatus = "Cancelled";
                    _tenantRepository.Update(tenant);
                }
            }
        }

        private async Task HandleSubscriptionPastDueAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var subscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(subscriptionId);
            
            if (existingSub != null)
            {
                existingSub.Status = ModelSubscriptionStatus.PastDue;
                existingSub.UpdatedAt = DateTime.UtcNow;
                _subscriptionRepository.Update(existingSub);

                var tenant = await _tenantRepository.GetByIdAsync(existingSub.TenantId);
                if (tenant != null)
                {
                    tenant.SubscriptionStatus = ModelSubscriptionStatus.PastDue;
                    tenant.OperationalStatus = "PastDue";
                    _tenantRepository.Update(tenant);
                }

                await SendPastDueEmailAsync(existingSub.TenantId);
            }
        }

        private async Task HandleInvoicePaymentSucceededAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var subscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(subscriptionId);
            
            if (existingSub != null && existingSub.Status == ModelSubscriptionStatus.PastDue)
            {
                existingSub.Status = ModelSubscriptionStatus.Active;
                existingSub.UpdatedAt = DateTime.UtcNow;
                _subscriptionRepository.Update(existingSub);

                var tenant = await _tenantRepository.GetByIdAsync(existingSub.TenantId);
                if (tenant != null)
                {
                    tenant.SubscriptionStatus = ModelSubscriptionStatus.Active;
                    tenant.OperationalStatus = "Active";
                    _tenantRepository.Update(tenant);
                }

                await SendPaymentRecoveredEmailAsync(existingSub.TenantId);
            }
        }

        private async Task HandleInvoicePaymentFailedAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var subscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(subscriptionId);
            
            if (existingSub != null)
            {
                existingSub.Status = ModelSubscriptionStatus.PastDue;
                existingSub.UpdatedAt = DateTime.UtcNow;
                _subscriptionRepository.Update(existingSub);

                var tenant = await _tenantRepository.GetByIdAsync(existingSub.TenantId);
                if (tenant != null)
                {
                    tenant.SubscriptionStatus = ModelSubscriptionStatus.PastDue;
                    tenant.OperationalStatus = "PastDue";
                    _tenantRepository.Update(tenant);
                }

                await SendPaymentFailedEmailAsync(existingSub.TenantId);
            }
        }

        private async Task HandleTrialWillEndAsync(IPaymentGateway gateway, PaymentWebhookEvent webhookEvent, string payload)
        {
            var subscriptionId = ExtractSubscriptionId(payload, gateway.GatewayType);
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(subscriptionId);
            
            if (existingSub != null)
            {
                await SendTrialEndingEmailAsync(existingSub.TenantId);
            }
        }

        private async Task UpsertSubscriptionAsync(SubscriptionInfo subscription, string? tenantId = null)
        {
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(subscription.GatewaySubscriptionId);
            var resolvedTenantId = tenantId ?? existingSub?.TenantId;

            if (string.IsNullOrWhiteSpace(resolvedTenantId))
            {
                _logger.LogWarning(
                    "Skipping subscription upsert for gateway subscription {GatewaySubscriptionId} because tenant resolution failed.",
                    subscription.GatewaySubscriptionId);
                return;
            }

            if (existingSub != null)
            {
                existingSub.Status = ConvertToModelStatus(subscription.Status);
                existingSub.CurrentPeriodStart = subscription.CurrentPeriodStart;
                existingSub.CurrentPeriodEnd = subscription.CurrentPeriodEnd;
                existingSub.TrialEndsAt = subscription.TrialEndsAt;
                existingSub.CancelledAt = subscription.CancelledAt;
                existingSub.UpdatedAt = DateTime.UtcNow;
                _subscriptionRepository.Update(existingSub);
            }
            else
            {
                var newSub = new Subscription
                {
                    TenantId = resolvedTenantId,
                    PeachSubscriptionId = subscription.GatewaySubscriptionId,
                    PeachPriceId = subscription.GatewayPriceId,
                    PricingPackageCode = subscription.PricingPackageCode,
                    Status = ConvertToModelStatus(subscription.Status),
                    CurrentPeriodStart = subscription.CurrentPeriodStart,
                    CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                    TrialEndsAt = subscription.TrialEndsAt,
                    CancelledAt = subscription.CancelledAt,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _subscriptionRepository.Add(newSub);
            }

            var tenant = await _tenantRepository.GetByIdAsync(resolvedTenantId);
            if (tenant != null)
            {
                tenant.SubscriptionStatus = ConvertToModelStatus(subscription.Status);
                tenant.SubscriptionEndsAt = subscription.CurrentPeriodEnd;
                tenant.OperationalStatus = subscription.Status == PaymentSubscriptionStatus.Active || subscription.Status == PaymentSubscriptionStatus.Trialing
                    ? "Active"
                    : subscription.Status == PaymentSubscriptionStatus.Cancelled ? "Cancelled" : "PastDue";
                _tenantRepository.Update(tenant);
            }
        }

        private static PaymentGateway GetGatewayFromSubscription(Subscription subscription)
        {
            if (string.IsNullOrWhiteSpace(subscription.PeachSubscriptionId))
            {
                return PaymentGateway.PeachPayments;
            }

            return subscription.PeachSubscriptionId.StartsWith("sub_") || subscription.PeachSubscriptionId.StartsWith("pf_")
                ? PaymentGateway.PayFast
                : PaymentGateway.PeachPayments;
        }

        private async Task<string?> ResolveTenantIdForSubscriptionAsync(IPaymentGateway gateway, string payload, string gatewaySubscriptionId)
        {
            var existingSub = await _subscriptionRepository.GetByPeachSubscriptionIdAsync(gatewaySubscriptionId);
            if (existingSub != null)
            {
                return existingSub.TenantId;
            }

            try
            {
                var json = JsonDocument.Parse(payload).RootElement;
                var tenantId = GetStringProperty(json, "tenant_id")
                    ?? GetStringProperty(json, "tenantId")
                    ?? GetStringProperty(json, "tenant")
                    ?? GetStringProperty(json, "tenant_uuid");

                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    return tenantId;
                }

                if (json.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == JsonValueKind.Object)
                {
                    var nestedTenantId = GetStringProperty(dataNode, "tenant_id")
                        ?? GetStringProperty(dataNode, "tenantId")
                        ?? GetStringProperty(dataNode, "tenant");

                    if (!string.IsNullOrWhiteSpace(nestedTenantId))
                    {
                        return nestedTenantId;
                    }
                }
            }
            catch
            {
                // Form-encoded PayFast payloads will not be valid JSON and therefore fall through to the final tenant lookup path.
            }

            var byPeachCustomer = await _peachCustomerRepository.Query()
                .FirstOrDefaultAsync(customer => customer.PeachCustomerId == gatewaySubscriptionId || customer.PeachCustomerId == payload);

            if (byPeachCustomer != null)
            {
                return byPeachCustomer.TenantId;
            }

            return null;
        }

        private static ModelSubscriptionStatus ConvertToModelStatus(PaymentSubscriptionStatus status)
        {
            return status switch
            {
                PaymentSubscriptionStatus.Trialing => ModelSubscriptionStatus.Trialing,
                PaymentSubscriptionStatus.Active => ModelSubscriptionStatus.Active,
                PaymentSubscriptionStatus.PastDue => ModelSubscriptionStatus.PastDue,
                PaymentSubscriptionStatus.Cancelled => ModelSubscriptionStatus.Cancelled,
                PaymentSubscriptionStatus.Expired => ModelSubscriptionStatus.Expired,
                PaymentSubscriptionStatus.Paused => ModelSubscriptionStatus.Paused,
                _ => ModelSubscriptionStatus.Trialing
            };
        }

        private async Task<CustomerInfo?> GetOrCreateGatewayCustomerAsync(IPaymentGateway gateway, string tenantId, string email, string name)
        {
            if (gateway.GatewayType == PaymentGateway.PayFast)
            {
                return new CustomerInfo
                {
                    GatewayCustomerId = email,
                    Email = email,
                    Name = name,
                    Gateway = PaymentGateway.PayFast
                };
            }

            var existingCustomer = await _peachCustomerRepository.GetByTenantIdAsync(tenantId);
            if (existingCustomer != null)
            {
                return new CustomerInfo
                {
                    GatewayCustomerId = existingCustomer.PeachCustomerId,
                    Email = existingCustomer.Email,
                    Name = name,
                    Gateway = gateway.GatewayType
                };
            }

            var customer = await gateway.GetOrCreateCustomerAsync(tenantId, email, name);
            if (customer != null)
            {
                var newCustomer = new PeachCustomer
                {
                    TenantId = tenantId,
                    PeachCustomerId = customer.GatewayCustomerId,
                    Email = customer.Email
                };
                _peachCustomerRepository.Add(newCustomer);
            }

            return customer;
        }

        private string ExtractSubscriptionId(string payload, PaymentGateway gateway)
        {
            try
            {
                var json = JsonDocument.Parse(payload).RootElement;
                
                return gateway switch
                {
                    PaymentGateway.PeachPayments => GetStringProperty(json.GetProperty("data").GetProperty("object"), "subscription"),
                    PaymentGateway.PayFast => GetStringProperty(json, "pf_payment_id") ?? GetStringProperty(json, "subscription_id"),
                    _ => string.Empty
                };
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task ProvisionTenantIfNewAsync(string tenantId, string pricingPackageCode)
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId);
            if (tenant == null) return;

            var existingAdmin = (await _userRepository.FindAsync(u => u.TenantId == tenantId && u.Email == tenant.ContactEmail)).FirstOrDefault();

            if (existingAdmin == null)
            {
                var adminUser = new UserAccount
                {
                    UserId = $"USR-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    TenantId = tenantId,
                    Email = tenant.ContactEmail,
                    FullName = "Admin User",
                    PasswordHash = "",
                    SystemRole = "AgencyAdmin",
                    AvatarUrl = string.Empty,
                    AccountStatus = "Active",
                    IsTrial = false,
                    CreatedAt = DateTime.UtcNow
                };
                _userRepository.Add(adminUser);
                _logger.LogInformation("Created admin user for new tenant {TenantId}", tenantId);
            }
        }

        private async Task SendWelcomeEmailAsync(string tenantId, string pricingPackageCode)
        {
            try
            {
                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant != null)
                {
                    await _emailService.SendEmailAsync(tenant.ContactEmail, "Welcome to Real Estate Enterprise", 
                        $"Your {pricingPackageCode} subscription is now active. Welcome aboard!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send welcome email for tenant {TenantId}", tenantId);
            }
        }

        private async Task SendPastDueEmailAsync(string tenantId)
        {
            try
            {
                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant != null)
                {
                    await _emailService.SendEmailAsync(tenant.ContactEmail, "Payment Past Due - Action Required",
                        "Your subscription payment is past due. Please update your payment method to avoid service interruption.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send past due email for tenant {TenantId}", tenantId);
            }
        }

        private async Task SendPaymentRecoveredEmailAsync(string tenantId)
        {
            try
            {
                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant != null)
                {
                    await _emailService.SendEmailAsync(tenant.ContactEmail, "Payment Recovered - Subscription Active",
                        "Your payment has been successfully processed. Your subscription is now active.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send payment recovered email for tenant {TenantId}", tenantId);
            }
        }

        private async Task SendPaymentFailedEmailAsync(string tenantId)
        {
            try
            {
                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant != null)
                {
                    await _emailService.SendEmailAsync(tenant.ContactEmail, "Payment Failed - Action Required",
                        "Your subscription payment failed. Please update your payment method to avoid service interruption.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send payment failed email for tenant {TenantId}", tenantId);
            }
        }

        private async Task SendTrialEndingEmailAsync(string tenantId)
        {
            try
            {
                var tenant = await _tenantRepository.GetByIdAsync(tenantId);
                if (tenant != null)
                {
                    await _emailService.SendEmailAsync(tenant.ContactEmail, "Your Trial Ends Soon",
                        "Your free trial ends in 3 days. Upgrade now to continue using all features.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send trial ending email for tenant {TenantId}", tenantId);
            }
        }

        private static string? GetStringProperty(JsonNode node, string propertyName, string? defaultValue = null)
        {
            if (node == null)
                return defaultValue;

            if (node[propertyName] != null)
                return node[propertyName]?.ToString();

            return defaultValue;
        }

        private static string? GetStringProperty(JsonElement element, string propertyName, string? defaultValue = null)
        {
            if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
                return defaultValue;

            if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
                return prop.GetString();

            return defaultValue;
        }

        private static string? GetStringProperty(JsonObject obj, string propertyName, string? defaultValue = null)
        {
            if (obj == null)
                return defaultValue;

            if (obj.TryGetPropertyValue(propertyName, out var node))
                return node?.ToString();

            return defaultValue;
        }
    }

    /// <summary>
    /// Response DTO for subscription status.
    /// </summary>
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
        public PaymentGateway? Gateway { get; init; }
    }
}