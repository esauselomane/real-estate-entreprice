using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Microservice.Services.Abstractions.Payments;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.Extensions.Logging;
using PaymentWebhookEvent = Identity.Microservice.Services.Abstractions.Payments.WebhookEvent;

namespace Identity.Microservice.Controllers
{
    [ApiController]
    [Route("api/payments/webhook")]
    [AllowAnonymous]
    public class WebhookController : ControllerBase
    {
        private readonly PaymentOrchestrationService _paymentOrchestrationService;
        private readonly IPaymentGatewayFactory _gatewayFactory;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            PaymentOrchestrationService paymentOrchestrationService,
            IPaymentGatewayFactory gatewayFactory,
            ILogger<WebhookController> logger)
        {
            _paymentOrchestrationService = paymentOrchestrationService;
            _gatewayFactory = gatewayFactory;
            _logger = logger;
        }

        /// <summary>
        /// Handle Peach Payments webhook events.
        /// </summary>
        [HttpPost("peach")]
        public async Task<IActionResult> HandlePeachWebhook()
        {
            return await HandleWebhookForGateway(PaymentGateway.PeachPayments);
        }

        /// <summary>
        /// Handle PayFast webhook events.
        /// </summary>
        [HttpPost("payfast")]
        public async Task<IActionResult> HandlePayFastWebhook()
        {
            return await HandleWebhookForGateway(PaymentGateway.PayFast);
        }

        /// <summary>
        /// Generic webhook endpoint that auto-detects gateway from headers/payload.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> HandleWebhook()
        {
            // Read raw body for signature verification
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync();

            // Try to detect gateway from headers
            var gateway = DetectGatewayFromHeaders();
            
            if (gateway == null)
            {
                // Try to detect from payload
                gateway = DetectGatewayFromPayload(payload);
            }

            if (gateway == null)
            {
                _logger.LogWarning("Could not detect payment gateway for webhook");
                return BadRequest(new { message = "Unable to determine payment gateway" });
            }

            return await HandleWebhookForGateway(gateway.Value, payload);
        }

        private async Task<IActionResult> HandleWebhookForGateway(PaymentGateway gateway, string? payload = null)
        {
            payload ??= await new StreamReader(Request.Body, Encoding.UTF8).ReadToEndAsync();

            var gatewayImpl = _gatewayFactory.GetGateway(gateway);
            
            // Verify webhook signature
            var isValid = await gatewayImpl.ValidateWebhookSignatureAsync(Request, payload);
            if (!isValid)
            {
                _logger.LogWarning("Webhook signature validation failed for {Gateway} from {RemoteIp}", gateway, HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { message = "Invalid webhook signature" });
            }

            // Parse and normalize webhook event
            var webhookEvent = gatewayImpl.ParseWebhookEvent(payload);
            webhookEvent = new PaymentWebhookEvent
            {
                GatewayEventId = webhookEvent.GatewayEventId,
                EventType = webhookEvent.EventType,
                PayloadJson = webhookEvent.PayloadJson,
                Gateway = gateway,
                ReceivedAt = webhookEvent.ReceivedAt
            };

            try
            {
                await _paymentOrchestrationService.ProcessWebhookEventAsync(webhookEvent, payload);
                _logger.LogInformation("Successfully processed webhook event {EventId} of type {EventType} from {Gateway}", 
                    webhookEvent.GatewayEventId, webhookEvent.EventType, gateway);
                return Ok(new { message = "Webhook processed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process webhook event {EventId} of type {EventType} from {Gateway}", 
                    webhookEvent.GatewayEventId, webhookEvent.EventType, gateway);
                return StatusCode(500, new { message = "Webhook processing failed" });
            }
        }

        private PaymentGateway? DetectGatewayFromHeaders()
        {
            // Peach Payments sends "Peach-Signature" header
            if (Request.Headers.ContainsKey("Peach-Signature"))
                return PaymentGateway.PeachPayments;

            // PayFast sends specific headers
            if (Request.Headers.ContainsKey("X-PayFast-Signature") || 
                Request.Headers.ContainsKey("signature"))
                return PaymentGateway.PayFast;

            return null;
        }

        private PaymentGateway? DetectGatewayFromPayload(string payload)
        {
            try
            {
                var json = JsonDocument.Parse(payload).RootElement;
                
                // Peach Payments has "type" field with values like "checkout.session.completed"
                if (json.TryGetProperty("type", out var typeProp))
                {
                    var type = typeProp.GetString();
                    if (type?.Contains("checkout.session") == true || 
                        type?.Contains("subscription.") == true ||
                        type?.Contains("invoice.") == true)
                        return PaymentGateway.PeachPayments;
                }

                // PayFast has "pf_payment_id" or "subscription_id"
                if (json.TryGetProperty("pf_payment_id", out _) || 
                    json.TryGetProperty("subscription_id", out _))
                    return PaymentGateway.PayFast;
            }
            catch
            {
                // Not JSON, could be form data from PayFast
                if (payload.Contains("pf_payment_id") || payload.Contains("subscription_id"))
                    return PaymentGateway.PayFast;
            }

            return null;
        }
    }
}