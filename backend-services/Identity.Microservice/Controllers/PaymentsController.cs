using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Microservice.DTOs;
using Identity.Microservice.Services.Abstractions.Payments;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.Extensions.Logging;
using DTOs = Identity.Microservice.DTOs;

namespace Identity.Microservice.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly PaymentOrchestrationService _paymentOrchestrationService;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            PaymentOrchestrationService paymentOrchestrationService,
            ILogger<PaymentsController> logger)
        {
            _paymentOrchestrationService = paymentOrchestrationService;
            _logger = logger;
        }

        /// <summary>
        /// Create a checkout session for subscription signup (gateway-agnostic).
        /// </summary>
        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] DTOs.CreateCheckoutSessionRequest request)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized(new { message = "Tenant context not found" });
            }

            try
            {
                var gatewayRequest = new CreateCheckoutRequest
                {
                    TenantId = tenantId,
                    PricingPackageCode = request.PricingPackageCode,
                    BillingCycle = Enum.Parse<BillingCycle>(request.BillingCycle, true),
                    SuccessUrl = request.SuccessUrl,
                    CancelUrl = request.CancelUrl,
                    PreferredGateway = null // Will use default gateway for tenant
                };

                var checkoutResult = await _paymentOrchestrationService.CreateCheckoutSessionAsync(gatewayRequest);

                return Ok(new CreateCheckoutSessionResponse
                {
                    CheckoutUrl = checkoutResult.CheckoutUrl,
                    SessionId = checkoutResult.SessionId,
                    ExpiresAt = checkoutResult.ExpiresAt
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create checkout session for tenant {TenantId}", tenantId);
                return StatusCode(500, new { message = "Failed to create checkout session", error = ex.Message });
            }
        }

        /// <summary>
        /// Create a billing portal session for subscription management (gateway-agnostic).
        /// </summary>
        [HttpPost("portal")]
        public async Task<IActionResult> CreatePortalSession([FromBody] CreatePortalSessionRequest request)
        {
            var tenantId = User.FindFirst("tenant_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(tenantId))
            {
                return Unauthorized(new { message = "Tenant context not found" });
            }

            try
            {
                var portalRequest = new CreatePortalRequest
                {
                    TenantId = tenantId,
                    ReturnUrl = request.ReturnUrl
                };

                var portalResult = await _paymentOrchestrationService.CreatePortalSessionAsync(portalRequest);
                
                return Ok(new CreatePortalSessionResponse
                {
                    PortalUrl = portalResult.PortalUrl,
                    SessionId = portalResult.SessionId
                });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create portal session for tenant {TenantId}", tenantId);
                return StatusCode(500, new { message = "Failed to create billing portal session", error = ex.Message });
            }
        }

        /// <summary>
        /// Get current subscription status for the authenticated tenant (gateway-agnostic).
        /// </summary>
        [HttpGet("subscription/status")]
        public async Task<IActionResult> GetSubscriptionStatus([FromQuery] string? tenantId = null)
        {
            var resolvedTenantId = tenantId ?? User.FindFirst("tenant_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(resolvedTenantId))
            {
                return Unauthorized(new { message = "Tenant context not found" });
            }

            var status = await _paymentOrchestrationService.GetSubscriptionStatusAsync(resolvedTenantId);
            return Ok(status);
        }

        [HttpGet("subscription/history")]
        public async Task<IActionResult> GetSubscriptionHistory([FromQuery] string? tenantId = null)
        {
            var resolvedTenantId = tenantId ?? User.FindFirst("tenant_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(resolvedTenantId))
            {
                return Unauthorized(new { message = "Tenant context not found" });
            }

            var history = await _paymentOrchestrationService.GetSubscriptionHistoryAsync(resolvedTenantId);
            return Ok(history);
        }

        /// <summary>
        /// Get available payment gateways.
        /// </summary>
        [HttpGet("gateways")]
        public async Task<IActionResult> GetAvailableGateways()
        {
            // This would be injected via IPaymentGatewayFactory
            return Ok(new { message = "Available via factory" });
        }

        /// <summary>
        /// Get webhook event history (admin only).
        /// </summary>
        [HttpGet("webhooks")]
        [Authorize(Roles = "SuperAdmin,PlatformAdmin")]
        public async Task<IActionResult> GetWebhookEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            // This would need a repository injected - keeping for admin visibility
            return Ok(new { message = "Use PaymentOrchestrationService for webhook management" });
        }
    }
}