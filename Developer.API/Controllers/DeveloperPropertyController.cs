using Developer.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Developer.API.Controllers
{
    [ApiController]
    [Route("api/developer/properties")]
    public class DeveloperPropertyController : ControllerBase
    {
        private readonly IGatewayClient _gateway;
        private readonly ILogger<DeveloperPropertyController> _logger;

        public DeveloperPropertyController(IGatewayClient gateway, ILogger<DeveloperPropertyController> logger)
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

        // Properties
        [HttpGet]
        public Task<IActionResult> SearchProperties([FromBody] object? searchBody = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.PostAsync($"api/properties/search?page={page}&pageSize={pageSize}", searchBody ?? new { }, agencyId, token));
        }

        [HttpGet("platforms")]
        public Task<IActionResult> GetPlatforms()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/platforms", agencyId, token));
        }

        [HttpGet("{id:int}")]
        public Task<IActionResult> GetProperty(int id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{id}", agencyId, token));
        }

        [HttpGet("active-listings-count")]
        public Task<IActionResult> GetActiveListingsCount()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/active-listings-count", agencyId, token));
        }

        // Units
        [HttpGet("{propertyId:int}/units")]
        public Task<IActionResult> GetUnits(int propertyId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units", agencyId, token));
        }

        [HttpGet("{propertyId:int}/units/{unitId:int}")]
        public Task<IActionResult> GetUnit(int propertyId, int unitId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units/{unitId}", agencyId, token));
        }

        [HttpGet("{propertyId:int}/units/{unitId:int}/documents")]
        public Task<IActionResult> GetUnitDocuments(int propertyId, int unitId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units/{unitId}/documents", agencyId, token));
        }

        [HttpGet("{propertyId:int}/units/{unitId:int}/documents/{storedName}")]
        public Task<IActionResult> GetUnitDocument(int propertyId, int unitId, string storedName)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units/{unitId}/documents/{Uri.EscapeDataString(storedName)}", agencyId, token));
        }

        [HttpGet("{propertyId:int}/units/{unitId:int}/maintenance")]
        public Task<IActionResult> GetUnitMaintenance(int propertyId, int unitId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units/{unitId}/maintenance", agencyId, token));
        }

        [HttpGet("{propertyId:int}/units/{unitId:int}/leases")]
        public Task<IActionResult> GetUnitLeases(int propertyId, int unitId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units/{unitId}/leases", agencyId, token));
        }

        [HttpGet("{propertyId:int}/units/{unitId:int}/leases/active")]
        public Task<IActionResult> GetUnitActiveLeases(int propertyId, int unitId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/units/{unitId}/leases/active", agencyId, token));
        }

        // Amenities
        [HttpGet("amenity-types")]
        public Task<IActionResult> GetAmenityTypes()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/amenity-types", agencyId, token));
        }

        [HttpGet("amenity-types/all")]
        public Task<IActionResult> GetAllAmenityTypes()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/amenity-types/all", agencyId, token));
        }

        [HttpGet("{propertyId:int}/amenities")]
        public Task<IActionResult> GetPropertyAmenities(int propertyId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/amenities", agencyId, token));
        }

        // Mandates
        [HttpGet("{propertyId:int}/mandates")]
        public Task<IActionResult> GetPropertyMandates(int propertyId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/mandates", agencyId, token));
        }

        [HttpGet("expiring-mandates")]
        public Task<IActionResult> GetExpiringMandates()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/expiring-mandates", agencyId, token));
        }

        // Syndication
        [HttpGet("syndication/providers")]
        public Task<IActionResult> GetSyndicationProviders()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/syndication/providers", agencyId, token));
        }

        [HttpGet("syndication/agencies/{tenantId}")]
        public Task<IActionResult> GetAgencySyndicationSettings(string tenantId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/syndication/agencies/{Uri.EscapeDataString(tenantId)}", agencyId, token));
        }

        [HttpGet("syndication/{propertyId:int}")]
        public Task<IActionResult> GetPropertySyndication(int propertyId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/syndication/{propertyId}", agencyId, token));
        }

        // Documents
        [HttpGet("{propertyId:int}/documents/{index:int}")]
        public Task<IActionResult> GetPropertyDocument(int propertyId, int index)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/{propertyId}/documents/{index}", agencyId, token));
        }

        // Import
        [HttpGet("import/template")]
        public Task<IActionResult> DownloadImportTemplate()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/properties/import/template", agencyId, token));
        }

        [HttpGet("import/jobs/{jobId}")]
        public Task<IActionResult> GetImportJobStatus(string jobId)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/properties/import/jobs/{Uri.EscapeDataString(jobId)}", agencyId, token));
        }
    }
}
