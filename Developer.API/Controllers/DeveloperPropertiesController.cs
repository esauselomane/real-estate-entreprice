using Developer.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Developer.API.Controllers
{
    [ApiController]
    [Route("api/developer/properties")]
    public class DeveloperPropertiesController : ControllerBase
    {
        private readonly IGatewayClient _gateway;
        private readonly ILogger<DeveloperPropertiesController> _logger;

        public DeveloperPropertiesController(IGatewayClient gateway, ILogger<DeveloperPropertiesController> logger)
        {
            _gateway = gateway;
            _logger = logger;
        }

        private string? GetAgencyId()
        {
            return HttpContext.Items.TryGetValue("AgencyId", out var v) ? v as string : null;
        }

        private string? GetServiceToken()
        {
            return HttpContext.Request.Headers.TryGetValue("X-Service-Token", out var v) ? v.ToString() : null;
        }

        private async Task<IActionResult> ProxyAsync(Task<HttpResponseMessage> responseTask)
        {
            var response = await responseTask;
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gateway request failed: {StatusCode} - {Content}", response.StatusCode, content);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return Unauthorized(new { message = "Authentication required" });
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return Forbid();
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return NotFound(new { message = "Resource not found" });

                return StatusCode((int)response.StatusCode, content);
            }

            return Content(content, "application/json");
        }

        [HttpGet]
        public Task<IActionResult> GetProperties([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties?page={page}&pageSize={pageSize}", agencyId, token));
        }

        [HttpGet("{id:int}")]
        public Task<IActionResult> GetProperty(int id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{id}", agencyId, token));
        }
    }
}
