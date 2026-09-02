using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
namespace Property.Microservice.Services
{
    public class PermissionCheckService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string _identityBaseUrl;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public PermissionCheckService(IHttpClientFactory httpFactory, IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        {
            _httpFactory = httpFactory;
            _identityBaseUrl = configuration["Identity:BaseUrl"] ?? "http://localhost:5001";
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _httpFactory.CreateClient();
                var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Replace("Bearer ", ""));
                }
                var request = new { permission };
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{_identityBaseUrl}/api/system/permissions/check", content, cancellationToken);
                if (!response.IsSuccessStatusCode) return false;
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("granted").GetBoolean();
            }
            catch
            {
                return false;
            }
        }
    }
}
