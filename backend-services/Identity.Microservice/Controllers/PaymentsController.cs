using System.Security.Claims;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Services.Abstractions.Payments;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(PaymentOrchestrationService payments, IPaymentGatewayFactory factory, IdentityDbContext database) : ControllerBase
{
    [HttpPost("create-checkout")]
    [Authorize(Roles = "AgencyAdmin,PlatformAdmin,SuperAdmin,Manager,Admin")]
    public async Task<IActionResult> CreateCheckoutSession(CreateCheckoutSessionRequest request)
    {
        var tenantId = ResolveTenant(request.TenantId);
        if (tenantId == null) return Forbid();
        if (!Enum.TryParse<BillingCycle>(request.BillingCycle, true, out var cycle) || !Enum.IsDefined(cycle))
            return BadRequest(new { message = "Choose monthly or annual billing." });
        try
        {
            var result = await payments.CreateCheckoutSessionAsync(new CreateCheckoutRequest
            {
                TenantId = tenantId, PricingPackageCode = request.PricingPackageCode,
                BillingCycle = cycle, SuccessUrl = request.SuccessUrl, CancelUrl = request.CancelUrl
            }, request.IdempotencyKey);
            return Ok(new CreateCheckoutSessionResponse
            {
                CheckoutUrl = result.CheckoutUrl, Method = result.Method, FormFields = result.FormFields,
                SessionId = result.SessionId, ExpiresAt = result.ExpiresAt
            });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPost("portal")]
    [Authorize(Roles = "AgencyAdmin,PlatformAdmin,SuperAdmin,Manager,Admin")]
    public async Task<IActionResult> CreatePortalSession(CreatePortalSessionRequest request)
    {
        var tenantId = ResolveTenant(request.TenantId);
        if (tenantId == null) return Forbid();
        try
        {
            var result = await payments.CreatePortalSessionAsync(new CreatePortalRequest { TenantId = tenantId, ReturnUrl = request.ReturnUrl });
            return Ok(new CreatePortalSessionResponse { PortalUrl = result.PortalUrl, SessionId = result.SessionId });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpGet("subscription/status")]
    public async Task<IActionResult> GetSubscriptionStatus(string? tenantId = null)
    {
        var resolved = ResolveTenant(tenantId);
        if (resolved == null) return Forbid();
        try { return Ok(await payments.GetSubscriptionStatusAsync(resolved)); }
        catch (ArgumentException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpGet("subscription/history")]
    public async Task<IActionResult> GetSubscriptionHistory(string? tenantId = null)
    {
        var resolved = ResolveTenant(tenantId);
        if (resolved == null) return Forbid();
        try { return Ok(await payments.GetSubscriptionHistoryAsync(resolved)); }
        catch (ArgumentException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpGet("gateways")]
    public IActionResult GetAvailableGateways() => Ok(factory.GetAllGateways().Select(provider => provider.GatewayType));

    [HttpGet("webhooks")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> GetWebhookEvents(int page = 1, int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var events = await database.WebhookEvents.AsNoTracking().OrderByDescending(item => item.ReceivedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(events.Select(ToWebhookDto));
    }

    [HttpPost("webhooks/{id:int}/retry")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> RetryWebhook(int id)
    {
        var item = await database.WebhookEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
        if (item?.Gateway == null) return NotFound();
        if (item.Processed) return Ok(new { message = "Already processed." });
        var provider = factory.GetGateway(item.Gateway.Value);
        // Only previously verified, stored payloads can be replayed by an administrator.
        await payments.ProcessWebhookEventAsync(provider.ParseWebhookEvent(item.PayloadJson), item.PayloadJson);
        return Ok(new { message = "Webhook replayed." });
    }

    [HttpPost("subscription/override")]
    [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
    public async Task<IActionResult> OverrideSubscription(BillingOverrideRequest request)
    {
        if (request.Reason.Trim().Length < 10 || !Enum.IsDefined(request.Status) || request.CurrentPeriodEnd == default)
            return BadRequest(new { message = "Supply a valid state, period end and a meaningful support reason." });
        var subscription = await database.Subscriptions.SingleOrDefaultAsync(item => item.Id == request.SubscriptionId && item.TenantId == request.TenantId);
        if (subscription == null) return NotFound();
        var tenant = await database.TenantAgencies.SingleAsync(item => item.TenantId == request.TenantId);
        var before = System.Text.Json.JsonSerializer.Serialize(new { subscription.Status, subscription.CurrentPeriodEnd, subscription.Gateway, subscription.PeachSubscriptionId });
        subscription.Status = request.Status;
        subscription.CurrentPeriodEnd = request.CurrentPeriodEnd;
        if (request.Gateway.HasValue)
        {
            if (!Enum.IsDefined(request.Gateway.Value) || string.IsNullOrWhiteSpace(request.GatewaySubscriptionId))
                return BadRequest(new { message = "Provider reconciliation requires a verified subscription identifier." });
            subscription.Gateway = request.Gateway;
            subscription.PeachSubscriptionId = request.GatewaySubscriptionId;
        }
        subscription.UpdatedAt = DateTime.UtcNow;
        tenant.SubscriptionStatus = request.Status;
        tenant.SubscriptionEndsAt = request.CurrentPeriodEnd;
        tenant.ActiveSubscriptionId = subscription.Id;
        tenant.IsTrial = request.Status == Identity.Microservice.Models.SubscriptionStatus.Trialing;
        tenant.TrialEndDate = tenant.IsTrial ? request.CurrentPeriodEnd : tenant.TrialEndDate;
        database.WebhookEvents.Add(new Identity.Microservice.Models.WebhookEvent
        {
            PeachEventId = $"override:{Guid.NewGuid():N}", EventType = "support.override", Processed = true,
            ProcessedAt = DateTime.UtcNow,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { actor = User.FindFirstValue(ClaimTypes.NameIdentifier), request, before })
        });
        await database.SaveChangesAsync();
        return Ok(await payments.GetSubscriptionStatusAsync(request.TenantId));
    }

    private static WebhookEventDto ToWebhookDto(Identity.Microservice.Models.WebhookEvent item) => new()
    {
        Id = item.Id, PeachEventId = item.PeachEventId, EventType = item.EventType,
        Processed = item.Processed, ReceivedAt = item.ReceivedAt, ProcessedAt = item.ProcessedAt, ErrorMessage = item.ErrorMessage
    };

    private string? ResolveTenant(string? requested)
    {
        var own = User.FindFirstValue("TenantId");
        if (string.IsNullOrWhiteSpace(own)) return null;
        if (string.IsNullOrWhiteSpace(requested) || requested == own) return own;
        return User.IsInRole("PlatformAdmin") || User.IsInRole("SuperAdmin") ? requested : null;
    }
}
