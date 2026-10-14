using Developer.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Developer.API.Controllers
{
    [ApiController]
    [Route("api/developer/crm")]
    public class DeveloperCrmController : ControllerBase
    {
        private readonly IGatewayClient _gateway;
        private readonly ILogger<DeveloperCrmController> _logger;

        public DeveloperCrmController(IGatewayClient gateway, ILogger<DeveloperCrmController> logger)
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

        // Agencies
        [HttpGet("agencies")]
        public Task<IActionResult> GetAgencies([FromQuery] int page = 1, [FromQuery] string? search = null)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = $"api/crm/agencies/roster?page={page}" + (search != null ? $"&search={Uri.EscapeDataString(search)}" : "");
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        [HttpGet("agencies/{id}")]
        public Task<IActionResult> GetAgency(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/agencies/{Uri.EscapeDataString(id)}", agencyId, token));
        }

        [HttpGet("agencies/{id}/contacts")]
        public Task<IActionResult> GetAgencyContacts(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/agencies/{Uri.EscapeDataString(id)}/contacts", agencyId, token));
        }

        // Brokers
        [HttpGet("brokers")]
        public Task<IActionResult> GetBrokers()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/crm/brokers", agencyId, token));
        }

        [HttpGet("brokers/{id}")]
        public Task<IActionResult> GetBroker(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/brokers/{Uri.EscapeDataString(id)}", agencyId, token));
        }

        [HttpGet("brokers/{id}/syndication-codes")]
        public Task<IActionResult> GetBrokerSyndicationCodes(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/brokers/{Uri.EscapeDataString(id)}/syndication-codes", agencyId, token));
        }

        // Contacts
        [HttpGet("contacts")]
        public Task<IActionResult> GetContacts([FromQuery] int page = 1, [FromQuery] string? contactType = null, [FromQuery] decimal? budgetMin = null, [FromQuery] decimal? budgetMax = null, [FromQuery] string? area = null)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = $"api/crm/contacts?page={page}";
            if (contactType != null) query += $"&contactType={Uri.EscapeDataString(contactType)}";
            if (budgetMin.HasValue) query += $"&budgetMin={budgetMin.Value}";
            if (budgetMax.HasValue) query += $"&budgetMax={budgetMax.Value}";
            if (area != null) query += $"&area={Uri.EscapeDataString(area)}";
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        [HttpPost("contacts/search")]
        public Task<IActionResult> SearchContacts([FromBody] object searchBody)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.PostAsync("api/crm/contacts/search", searchBody, agencyId, token));
        }

        [HttpGet("contacts/{id}")]
        public Task<IActionResult> GetContact(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/contacts/{Uri.EscapeDataString(id)}", agencyId, token));
        }

        // Companies
        [HttpGet("companies")]
        public Task<IActionResult> GetCompanies([FromQuery] int page = 1)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/companies?page={page}", agencyId, token));
        }

        [HttpGet("companies/search")]
        public Task<IActionResult> SearchCompanies([FromQuery] string? search = null, [FromQuery] int page = 1)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = $"api/crm/companies/search?page={page}" + (search != null ? $"&search={Uri.EscapeDataString(search)}" : "");
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        [HttpGet("companies/{id}")]
        public Task<IActionResult> GetCompany(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/companies/{Uri.EscapeDataString(id)}", agencyId, token));
        }

        // Legal Entities
        [HttpGet("legal-entities")]
        public Task<IActionResult> GetLegalEntities([FromQuery] int page = 1)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/legal-entities?page={page}", agencyId, token));
        }

        [HttpGet("legal-entities/search")]
        public Task<IActionResult> SearchLegalEntities([FromQuery] string? search = null, [FromQuery] int page = 1)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = $"api/crm/legal-entities/search?page={page}" + (search != null ? $"&search={Uri.EscapeDataString(search)}" : "");
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        [HttpGet("legal-entities/{id}")]
        public Task<IActionResult> GetLegalEntity(string id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/legal-entities/{Uri.EscapeDataString(id)}", agencyId, token));
        }

        // Geography
        [HttpGet("geography")]
        public Task<IActionResult> GetGeography([FromQuery] string? search = null)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = "api/crm/geography" + (search != null ? $"?search={Uri.EscapeDataString(search)}" : "");
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        [HttpGet("geography/{id:int}")]
        public Task<IActionResult> GetGeographyById(int id)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/geography/{id}", agencyId, token));
        }

        // Operations
        [HttpGet("operations")]
        public Task<IActionResult> GetOperations([FromQuery] int page = 1, [FromQuery] string? module = null, [FromQuery] int? propertyId = null, [FromQuery] int? propertyUnitId = null)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = $"api/crm/operations?page={page}";
            if (module != null) query += $"&module={Uri.EscapeDataString(module)}";
            if (propertyId.HasValue) query += $"&propertyId={propertyId.Value}";
            if (propertyUnitId.HasValue) query += $"&propertyUnitId={propertyUnitId.Value}";
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        // Boards
        [HttpGet("boards")]
        public Task<IActionResult> GetBoards([FromQuery] string? parameters = null)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            var query = "api/crm/boards" + (parameters != null ? $"?{parameters}" : "");
            return ProxyAsync(_gateway.GetAsync(query, agencyId, token));
        }

        // Leads - Broker suggestions
        [HttpGet("brokers/suggested")]
        public Task<IActionResult> GetSuggestedBrokers()
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync("api/crm/brokers/suggested", agencyId, token));
        }

        // Notifications
        [HttpGet("notifications")]
        public Task<IActionResult> GetNotifications([FromQuery] int page = 1)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/notifications?page={page}", agencyId, token));
        }

        // Compliance
        [HttpGet("compliance/inbox")]
        public Task<IActionResult> GetComplianceInbox([FromQuery] int page = 1)
        {
            var agencyId = GetAgencyId();
            var token = GetServiceToken();
            return ProxyAsync(_gateway.GetAsync($"api/crm/compliance/inbox?page={page}", agencyId, token));
        }
    }
}
