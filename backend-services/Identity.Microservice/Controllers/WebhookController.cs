using System.Text;
using Identity.Microservice.Services.Abstractions.Payments;
using Identity.Microservice.Services.Implementation.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Microservice.Controllers;

[ApiController]
[Route("api/payments/webhook")]
[AllowAnonymous]
public sealed class WebhookController(IPaymentGatewayFactory factory, PaymentOrchestrationService payments, ILogger<WebhookController> logger) : ControllerBase
{
    [HttpPost("payfast")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> HandlePayFastWebhook() => HandleAsync(PaymentGateway.PayFast);

    [HttpPost("peach")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> HandlePeachWebhook() => HandleAsync(PaymentGateway.PeachPayments);

    private async Task<IActionResult> HandleAsync(PaymentGateway gateway)
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var payload = await reader.ReadToEndAsync(HttpContext.RequestAborted);
            var provider = factory.GetGateway(gateway);
            if (!await provider.ValidateWebhookSignatureAsync(Request, payload, HttpContext.RequestAborted))
                return Unauthorized(new { message = "Invalid payment notification." });
            var paymentEvent = provider.ParseWebhookEvent(payload);
            await payments.ProcessWebhookEventAsync(paymentEvent, payload);
            return Ok(new { message = "Notification processed." });
        }
        catch (ArgumentException exception)
        {
            logger.LogWarning(exception, "Rejected {Gateway} payment notification", gateway);
            return BadRequest(new { message = "Notification requires review." });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to process {Gateway} notification", gateway);
            return StatusCode(503, new { message = "Notification processing unavailable; retry later." });
        }
    }
}
