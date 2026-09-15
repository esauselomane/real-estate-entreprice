using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Identity.Microservice.Configuration;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentWebhookEvent = Identity.Microservice.Services.Abstractions.Payments.WebhookEvent;
using StoredWebhookEvent = Identity.Microservice.Models.WebhookEvent;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>Owns pricing, tenant billing state and atomic processing of verified payment events.</summary>
public sealed class PaymentOrchestrationService(
    IdentityDbContext database,
    IPaymentGatewayFactory factory,
    IOptions<PaymentSettings> settings,
    ILogger<PaymentOrchestrationService> logger)
{
    public async Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, string idempotencyKey)
    {
        ValidateReturnUrl(request.SuccessUrl);
        ValidateReturnUrl(request.CancelUrl);
        if (!Guid.TryParse(idempotencyKey, out _))
            throw new ArgumentException("A UUID idempotency key is required.");
        if (!Enum.IsDefined(request.BillingCycle))
            throw new ArgumentException("Choose monthly or annual billing.");
        if (request.PricingPackageCode.Equals("ENTERPRISE", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Enterprise subscriptions are arranged through sales.");

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await LockTenantAsync(request.TenantId);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.TenantId}:{idempotencyKey}")));
        var existing = await database.PaymentIntents.SingleOrDefaultAsync(intent => intent.IdempotencyKey == key);
        if (existing != null)
        {
            if (existing.Status == PaymentIntentStatus.Succeeded)
                throw new ArgumentException("This checkout has already completed.");
            var previous = JsonSerializer.Deserialize<CreateCheckoutRequest>(existing.MetadataJson!);
            if (previous?.PricingPackageCode != request.PricingPackageCode || previous.BillingCycle != request.BillingCycle)
                throw new ArgumentException("This checkout key belongs to a different plan or billing cycle.");
            if (existing.CheckoutResponseJson != null && existing.CheckoutExpiresAt > DateTime.UtcNow)
                return JsonSerializer.Deserialize<CreateCheckoutResult>(existing.CheckoutResponseJson)!;
            throw new ArgumentException("This checkout has expired. Start a new checkout.");
        }
        var tenant = await GetTenantAsync(request.TenantId);
        var package = await database.PricingPackages.SingleOrDefaultAsync(package => package.Code == request.PricingPackageCode && package.IsActive)
            ?? throw new ArgumentException("Invalid or inactive pricing package.");
        var current = await LatestSubscriptionAsync(tenant.TenantId);
        if (current?.Status == SubscriptionStatus.Active && current.CurrentPeriodEnd > DateTime.UtcNow)
            throw new ArgumentException("Manage your existing subscription through billing support before changing plans.");
        if (current is { Gateway: null, Status: not SubscriptionStatus.Trialing } && !current.PeachSubscriptionId.StartsWith("trial-"))
            throw new ArgumentException("Your existing provider must be reconciled by billing support before checkout.");
        if (current?.Status is SubscriptionStatus.PastDue or SubscriptionStatus.Paused)
            throw new ArgumentException("Recover your existing subscription using Manage Billing.");
        var pending = await database.PaymentIntents.Where(intent => intent.TenantId == tenant.TenantId && intent.Status == PaymentIntentStatus.Processing && intent.CheckoutExpiresAt > DateTime.UtcNow)
            .OrderByDescending(intent => intent.CreatedAt).FirstOrDefaultAsync();
        if (pending?.CheckoutResponseJson != null)
        {
            var pendingRequest = JsonSerializer.Deserialize<CreateCheckoutRequest>(pending.MetadataJson!)!;
            if (pendingRequest.PricingPackageCode != request.PricingPackageCode || pendingRequest.BillingCycle != request.BillingCycle)
                throw new ArgumentException("A checkout for another plan is still open. Finish it or wait for it to expire before changing plans.");
            return JsonSerializer.Deserialize<CreateCheckoutResult>(pending.CheckoutResponseJson)!;
        }
        var provider = factory.GetGatewayForTenant(tenant.TenantId, current?.Gateway);
        var amount = request.BillingCycle == BillingCycle.Annual ? package.AnnualPrice : package.MonthlyPrice;
        if (amount is null or <= 0)
            throw new ArgumentException("This package requires a sales quote.");
        var gatewayRequest = request with
        {
            Amount = amount.Value,
            MerchantReference = Guid.NewGuid().ToString("N"),
            CustomerEmail = tenant.ContactEmail,
            CustomerName = tenant.AgencyName,
            PreferredGateway = provider.GatewayType
        };
        var result = await provider.CreateCheckoutSessionAsync(gatewayRequest);
        database.PaymentIntents.Add(new PaymentIntent
        {
            TenantId = tenant.TenantId,
            PeachCheckoutId = gatewayRequest.MerchantReference,
            Amount = amount.Value,
            Gateway = provider.GatewayType,
            Status = PaymentIntentStatus.Processing,
            IdempotencyKey = key,
            MetadataJson = JsonSerializer.Serialize(gatewayRequest),
            CheckoutResponseJson = JsonSerializer.Serialize(result),
            CheckoutExpiresAt = result.ExpiresAt
        });
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }

    public async Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request)
    {
        ValidateReturnUrl(request.ReturnUrl);
        var subscription = await LatestSubscriptionAsync(request.TenantId)
            ?? throw new ArgumentException("No subscription is available to manage.");
        if (subscription.Gateway == null)
            throw new ArgumentException("Billing support must reconcile the provider for this subscription.");
        return await factory.GetGateway(subscription.Gateway.Value).CreatePortalSessionAsync(request with
        {
            GatewaySubscriptionId = subscription.PeachSubscriptionId
        });
    }

    public async Task<SubscriptionStatusResponse> GetSubscriptionStatusAsync(string tenantId)
    {
        var tenant = await GetTenantAsync(tenantId);
        var subscription = await LatestSubscriptionAsync(tenantId);
        var package = subscription == null ? null : await database.PricingPackages.SingleOrDefaultAsync(package => package.Code == subscription.PricingPackageCode);
        var result = PaymentStateHelper.Describe(tenant, subscription, package, DateTime.UtcNow);
        var provider = factory.GetAllGateways().FirstOrDefault(provider => provider.GatewayType == result.Gateway);
        return result with { CanManageBilling = result.CanManageBilling && provider?.SupportsPortal == true };
    }

    public async Task<List<SubscriptionStatusResponse>> GetSubscriptionHistoryAsync(string tenantId)
    {
        var tenant = await GetTenantAsync(tenantId);
        var subscriptions = await database.Subscriptions.Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => subscription.CreatedAt).ToListAsync();
        var result = new List<SubscriptionStatusResponse>();
        foreach (var subscription in subscriptions)
        {
            var package = await database.PricingPackages.SingleOrDefaultAsync(package => package.Code == subscription.PricingPackageCode);
            result.Add(PaymentStateHelper.Describe(tenant, subscription, package, DateTime.UtcNow));
        }
        return result;
    }

    /// <summary>Serializes each tenant's events. Only a validated merchant reference can establish tenant ownership.</summary>
    public async Task ProcessWebhookEventAsync(PaymentWebhookEvent paymentEvent, string payload)
    {
        var eventKey = $"{(int)paymentEvent.Gateway}:{paymentEvent.GatewayEventId}";
        var intent = await database.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(intent => intent.PeachCheckoutId == paymentEvent.MerchantReference && intent.Gateway == paymentEvent.Gateway);
        if (intent == null && !string.IsNullOrWhiteSpace(paymentEvent.GatewaySubscriptionId))
            intent = await database.PaymentIntents.AsNoTracking().SingleOrDefaultAsync(item => item.PeachPaymentId == paymentEvent.GatewaySubscriptionId && item.Gateway == paymentEvent.Gateway);
        if (intent == null)
        {
            var exception = new ArgumentException("Webhook merchant reference does not match a payment intent.");
            await RecordFailureAsync(eventKey, paymentEvent, payload, exception);
            throw exception;
        }
        try
        {
            await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await LockTenantAsync(intent.TenantId);
            var storedEvent = await database.WebhookEvents.SingleOrDefaultAsync(item => item.PeachEventId == eventKey);
            if (storedEvent?.Processed == true)
                return;
            storedEvent ??= new StoredWebhookEvent { PeachEventId = eventKey, Gateway = paymentEvent.Gateway, EventType = paymentEvent.EventType, PayloadJson = payload };
            if (storedEvent.Id == 0)
                database.WebhookEvents.Add(storedEvent);
            var trackedIntent = await database.PaymentIntents.SingleAsync(item => item.Id == intent.Id);
            await ApplyEventAsync(paymentEvent, trackedIntent);
            storedEvent.Processed = true;
            storedEvent.ProcessedAt = DateTime.UtcNow;
            storedEvent.ErrorMessage = null;
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception exception)
        {
            database.ChangeTracker.Clear();
            await RecordFailureAsync(eventKey, paymentEvent, payload, exception);
            throw;
        }
    }

    private async Task ApplyEventAsync(PaymentWebhookEvent paymentEvent, PaymentIntent intent)
    {
        if (string.IsNullOrWhiteSpace(paymentEvent.GatewaySubscriptionId) && paymentEvent.SubscriptionStatus != SubscriptionStatus.PastDue)
            throw new ArgumentException("Recurring payment notification is missing its subscription token.");
        if (paymentEvent.SubscriptionStatus == null)
            throw new ArgumentException("Unsupported subscription event; manual review required.");
        var request = JsonSerializer.Deserialize<CreateCheckoutRequest>(intent.MetadataJson!)!;
        var subscription = await database.Subscriptions.SingleOrDefaultAsync(item => item.PeachSubscriptionId == paymentEvent.GatewaySubscriptionId && item.Gateway == paymentEvent.Gateway);
        if (subscription != null && subscription.TenantId != intent.TenantId)
            throw new ArgumentException("Subscription ownership mismatch.");
        var now = DateTime.UtcNow;
        if (subscription?.Status == SubscriptionStatus.Cancelled && paymentEvent.SubscriptionStatus != SubscriptionStatus.Cancelled)
            throw new ArgumentException("A cancelled subscription requires manual reconciliation.");
        if (paymentEvent.SubscriptionStatus == SubscriptionStatus.PastDue)
        {
            var completedKey = $"{(int)paymentEvent.Gateway}:{paymentEvent.GatewayEventId.Replace(":FAILED", ":COMPLETE")}";
            if (await database.WebhookEvents.AnyAsync(item => item.PeachEventId == completedKey && item.Processed))
                return;
        }
        if (paymentEvent.SubscriptionStatus == SubscriptionStatus.Active)
        {
            if (paymentEvent.Amount != intent.Amount)
                throw new ArgumentException("Payment amount does not match the subscription agreement.");
            if (subscription?.Status == SubscriptionStatus.Cancelled)
                throw new ArgumentException("Payment received for cancelled subscription; review required.");
            if (subscription == null)
            {
                if (await database.Subscriptions.AnyAsync(item => item.TenantId == intent.TenantId && item.Status == SubscriptionStatus.Active && item.CurrentPeriodEnd > now))
                    throw new ArgumentException("A different subscription is already active; review this additional payment.");
                subscription = new Subscription
                {
                    TenantId = intent.TenantId, Gateway = paymentEvent.Gateway,
                    PeachSubscriptionId = paymentEvent.GatewaySubscriptionId,
                    PeachPriceId = request.PricingPackageCode,
                    PricingPackageCode = request.PricingPackageCode, CurrentPeriodStart = now
                };
                database.Subscriptions.Add(subscription);
                var trials = await database.Subscriptions.Where(item => item.TenantId == intent.TenantId && item.Status == SubscriptionStatus.Trialing).ToListAsync();
                foreach (var trial in trials)
                {
                    trial.Status = SubscriptionStatus.Expired;
                    trial.CurrentPeriodEnd = now;
                }
            }
            var periodStart = subscription.CurrentPeriodEnd > now ? subscription.CurrentPeriodEnd : now;
            subscription.CurrentPeriodEnd = request.BillingCycle == BillingCycle.Annual ? periodStart.AddYears(1) : periodStart.AddMonths(1);
            intent.PeachPaymentId = paymentEvent.GatewaySubscriptionId;
            intent.Status = PaymentIntentStatus.Succeeded;
            intent.CompletedAt = now;
        }
        if (subscription == null && paymentEvent.SubscriptionStatus == SubscriptionStatus.PastDue)
        {
            intent.Status = PaymentIntentStatus.Failed;
            var trialTenant = await GetTenantAsync(intent.TenantId);
            QueueNotification(trialTenant, SubscriptionStatus.Trialing, SubscriptionStatus.PastDue, paymentEvent.GatewayEventId);
            return;
        }
        if (subscription == null)
            throw new ArgumentException("Subscription is not yet established by a successful payment.");
        var previousStatus = subscription.Status;
        subscription.Status = paymentEvent.SubscriptionStatus.Value;
        subscription.UpdatedAt = now;
        if (subscription.Status == SubscriptionStatus.Cancelled)
            subscription.CancelledAt = now;
        var tenant = await GetTenantAsync(intent.TenantId);
        tenant.SubscriptionStatus = subscription.Status;
        tenant.SubscriptionEndsAt = subscription.CurrentPeriodEnd;
        tenant.OperationalStatus = subscription.Status.ToString();
        if (subscription.Status == SubscriptionStatus.Active)
        {
            tenant.IsTrial = false;
            var users = await database.UserAccounts.Where(user => user.TenantId == tenant.TenantId && user.IsTrial).ToListAsync();
            foreach (var user in users)
                user.IsTrial = false;
        }
        await database.SaveChangesAsync();
        tenant.ActiveSubscriptionId = subscription.Id;
        QueueNotification(tenant, previousStatus, subscription.Status, paymentEvent.GatewayEventId);
    }

    private void QueueNotification(TenantAgency tenant, SubscriptionStatus previous, SubscriptionStatus current, string eventId)
    {
        if (previous == current) return;
        var template = current switch
        {
            SubscriptionStatus.Active when previous == SubscriptionStatus.PastDue => "recovered",
            SubscriptionStatus.Active => "welcome",
            SubscriptionStatus.PastDue => "payment-failed",
            SubscriptionStatus.Paused => "past-due",
            SubscriptionStatus.Cancelled => "cancelled",
            _ => "update"
        };
        database.BillingNotifications.Add(new BillingNotification
        {
            EventKey = $"payment:{eventId}", Recipient = tenant.ContactEmail, Template = template
        });
        logger.LogInformation("Subscription {EventId} for tenant {TenantId}: {Previous} -> {Current}", eventId, tenant.TenantId, previous, current);
    }

    private async Task RecordFailureAsync(string key, PaymentWebhookEvent paymentEvent, string payload, Exception exception)
    {
        var item = await database.WebhookEvents.SingleOrDefaultAsync(item => item.PeachEventId == key);
        if (item?.Processed == true)
            return;
        if (item == null)
        {
            item = new StoredWebhookEvent { PeachEventId = key, Gateway = paymentEvent.Gateway, EventType = paymentEvent.EventType, PayloadJson = payload };
            database.WebhookEvents.Add(item);
        }
        item.ErrorMessage = exception.Message[..Math.Min(exception.Message.Length, 2000)];
        await database.SaveChangesAsync();
    }

    private async Task LockTenantAsync(string tenantId)
    {
        if (database.Database.IsSqlServer())
        {
            var resource = $"subscription:{tenantId}";
            await database.Database.ExecuteSqlInterpolatedAsync($"DECLARE @result int; EXEC @result = sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 50001, 'Billing is busy. Retry the request.', 1;");
        }
    }

    private async Task<TenantAgency> GetTenantAsync(string tenantId) =>
        await database.TenantAgencies.SingleOrDefaultAsync(tenant => tenant.TenantId == tenantId)
        ?? throw new ArgumentException("Tenant not found.");

    private Task<Subscription?> LatestSubscriptionAsync(string tenantId) =>
        database.Subscriptions.Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => subscription.CreatedAt).ThenByDescending(subscription => subscription.Id).FirstOrDefaultAsync();

    private void ValidateReturnUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 ||
            !settings.Value.AllowedReturnOrigins.Contains(uri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Return URL is not an approved application origin.");
    }
}
