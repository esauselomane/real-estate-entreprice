using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Identity.Microservice.Configuration;
using Identity.Microservice.Models;
using Identity.Microservice.Services.Abstractions.Payments;
using Microsoft.Extensions.Options;
using PaymentWebhookEvent = Identity.Microservice.Services.Abstractions.Payments.WebhookEvent;

namespace Identity.Microservice.Services.Implementation.Payments;

/// <summary>PayFast hosted recurring card checkout and verified ITN adapter.</summary>
public sealed class PayFastGateway(HttpClient httpClient, IOptions<PayFastSettings> options, ILogger<PayFastGateway> logger)
    : PaymentGatewayBase(httpClient, logger)
{
    private readonly PayFastSettings settings = options.Value;
    public override bool SupportsPortal => !settings.BaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase);
    public override PaymentGateway GatewayType => PaymentGateway.PayFast;

    public override Task<CreateCheckoutResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.MerchantId) || string.IsNullOrWhiteSpace(settings.MerchantKey) || string.IsNullOrWhiteSpace(settings.PassPhrase))
            throw new InvalidOperationException("PayFast recurring billing credentials and passphrase must be configured.");
        if (!Uri.TryCreate(settings.WebhookUrl, UriKind.Absolute, out var webhook) || webhook.Scheme != "https" || webhook.Host.EndsWith("payfast.co.za"))
            throw new InvalidOperationException("Configure the public HTTPS application PayFast webhook URL.");
        if (request.Amount <= 0 || request.RecurringAmount is <= 0)
            throw new ArgumentException("A positive checkout amount is required.");
        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = settings.MerchantId,
            ["merchant_key"] = settings.MerchantKey,
            ["return_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl,
            ["notify_url"] = settings.WebhookUrl,
            ["name_first"] = request.CustomerName ?? "Customer",
            ["email_address"] = request.CustomerEmail ?? string.Empty,
            ["m_payment_id"] = request.MerchantReference,
            ["amount"] = request.Amount.ToString("F2", CultureInfo.InvariantCulture),
            ["item_name"] = $"{request.PricingPackageCode} subscription",
            ["payment_method"] = "cc",
            ["subscription_type"] = "1",
            ["billing_date"] = FirstRecurringBillingDate(request).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["recurring_amount"] = (request.RecurringAmount ?? request.Amount).ToString("F2", CultureInfo.InvariantCulture),
            ["frequency"] = request.BillingCycle == BillingCycle.Monthly ? "3" : "6",
            ["cycles"] = "0",
            ["subscription_notify_email"] = "true",
            ["subscription_notify_webhook"] = "true",
            ["subscription_notify_buyer"] = "true"
        };
        if (request.RecurringAmount.HasValue && request.RecurringAmount != request.Amount)
            fields["item_description"] = $"Initial payment R{request.Amount.ToString("F2", CultureInfo.InvariantCulture)}; renews at R{request.RecurringAmount.Value.ToString("F2", CultureInfo.InvariantCulture)} {request.BillingCycle.ToString().ToLowerInvariant()}.";
        fields["signature"] = PayFastSignatureHelper.Sign(fields, settings.PassPhrase);
        return Task.FromResult(new CreateCheckoutResult
        {
            CheckoutUrl = settings.ProcessUrl, Method = "POST", FormFields = fields,
            SessionId = request.MerchantReference, ExpiresAt = DateTime.UtcNow.AddMinutes(30), Gateway = GatewayType
        });
    }

    private static DateTime FirstRecurringBillingDate(CreateCheckoutRequest request)
    {
        var today = DateTime.UtcNow.Date;
        if (!request.RecurringAmount.HasValue || request.RecurringAmount == request.Amount) return today;
        return request.BillingCycle == BillingCycle.Annual ? today.AddYears(1) : today.AddMonths(1);
    }

    public override Task<CreatePortalResult> CreatePortalSessionAsync(CreatePortalRequest request, CancellationToken cancellationToken = default)
    {
        if (!SupportsPortal)
            throw new ArgumentException("PayFast does not support card updates in sandbox. Use the sandbox dashboard or contact billing support.");
        if (string.IsNullOrWhiteSpace(request.GatewaySubscriptionId))
            throw new ArgumentException("Subscription token is required.");
        return Task.FromResult(new CreatePortalResult
        {
            PortalUrl = $"{settings.BaseUrl}/eng/recurring/update/{Uri.EscapeDataString(request.GatewaySubscriptionId)}?return={PayFastSignatureHelper.Encode(request.ReturnUrl)}",
            Gateway = GatewayType
        });
    }

    public override Task<CustomerInfo?> GetOrCreateCustomerAsync(string tenantId, string email, string name, CancellationToken cancellationToken = default) =>
        Task.FromResult<CustomerInfo?>(new CustomerInfo { GatewayCustomerId = email, Email = email, Name = name, Gateway = GatewayType });

    public override Task<SubscriptionInfo?> GetSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Subscription state is maintained from validated PayFast notifications.");

    public override Task<SubscriptionInfo?> CancelSubscriptionAsync(string gatewaySubscriptionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Cancellation must be confirmed in PayFast merchant billing and synchronized by notification.");

    public override async Task<bool> ValidateWebhookSignatureAsync(HttpRequest request, string payload, CancellationToken cancellationToken = default)
    {
        var fields = PayFastSignatureHelper.Parse(payload);
        if (!fields.TryGetValue("signature", out var signature) || string.IsNullOrWhiteSpace(settings.PassPhrase) ||
            !fields.TryGetValue("merchant_id", out var merchantId) || merchantId != settings.MerchantId)
            return false;
        var expected = PayFastSignatureHelper.Sign(fields, settings.PassPhrase, includeEmptyFields: true);
        if (!ConstantTimeEquals(signature, expected))
            return false;
        // Provider postback verifies origin independently of reverse-proxy client IP headers.
        var result = await PaymentHttpHelper.PostValidationAsync(HttpClient, $"{settings.BaseUrl}/eng/query/validate", fields, cancellationToken);
        return result.Trim() == "VALID";
    }

    public override PaymentWebhookEvent ParseWebhookEvent(string payload)
    {
        var fields = PayFastSignatureHelper.Parse(payload);
        var paymentStatus = fields.GetValueOrDefault("payment_status")?.ToUpperInvariant();
        var state = paymentStatus ?? fields.GetValueOrDefault("subscription_status")?.ToUpperInvariant();
        var status = state switch
        {
            "COMPLETE" => SubscriptionStatus.Active,
            "FAILED" => SubscriptionStatus.PastDue,
            "CANCELLED" => SubscriptionStatus.Cancelled,
            "SUSPENDED" => SubscriptionStatus.Paused,
            "EXPIRED" => SubscriptionStatus.Expired,
            _ => (SubscriptionStatus?)null
        };
        var paymentId = fields.GetValueOrDefault("pf_payment_id");
        var identifier = string.IsNullOrWhiteSpace(paymentId)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            : paymentId;
        decimal? amount = decimal.TryParse(fields.GetValueOrDefault("amount_gross"), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return new PaymentWebhookEvent
        {
            Gateway = GatewayType, GatewayEventId = $"{identifier}:{state}",
            EventType = state ?? "UNKNOWN", PayloadJson = payload,
            MerchantReference = fields.GetValueOrDefault("m_payment_id") ?? string.Empty,
            GatewaySubscriptionId = fields.GetValueOrDefault("token") ?? string.Empty,
            Amount = amount, SubscriptionStatus = status
        };
    }

    public override string GetPriceId(string pricingPackageCode, BillingCycle billingCycle) => $"{pricingPackageCode}:{billingCycle}";
}
