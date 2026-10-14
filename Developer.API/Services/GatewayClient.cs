using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Developer.API.Services;

namespace Developer.API.Services
{
    public class GatewayClient : IGatewayClient
    {
        private readonly HttpClient _http;
        private readonly string _defaultServiceToken;
        private readonly ILogger<GatewayClient> _logger;

        public GatewayClient(HttpClient http, IConfiguration config, ILogger<GatewayClient> logger)
        {
            _http = http;
            _defaultServiceToken = config["Gateway:ServiceToken"] ?? string.Empty;
            _logger = logger;
        }

        private void AddHeaders(HttpRequestMessage request, string? agencyId, string? serviceToken)
        {
            var token = serviceToken ?? _defaultServiceToken;
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            if (!string.IsNullOrWhiteSpace(agencyId))
            {
                request.Headers.Add("X-Agency-Id", agencyId);
            }

            request.Headers.Add("X-Forwarded-For", "developer-api");
        }

        public async Task<HttpResponseMessage> GetAsync(string path, string? agencyId = null, string? serviceToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            AddHeaders(request, agencyId, serviceToken);
            _logger.LogDebug("Gateway GET {Path} AgencyId={AgencyId}", path, agencyId);
            return await _http.SendAsync(request);
        }

        public async Task<HttpResponseMessage> PostAsync(string path, object? body, string? agencyId = null, string? serviceToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            if (body != null)
            {
                request.Content = JsonContent.Create(body);
            }
            AddHeaders(request, agencyId, serviceToken);
            _logger.LogDebug("Gateway POST {Path} AgencyId={AgencyId}", path, agencyId);
            return await _http.SendAsync(request);
        }

        public async Task<HttpResponseMessage> PutAsync(string path, object? body, string? agencyId = null, string? serviceToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, path);
            if (body != null)
            {
                request.Content = JsonContent.Create(body);
            }
            AddHeaders(request, agencyId, serviceToken);
            _logger.LogDebug("Gateway PUT {Path} AgencyId={AgencyId}", path, agencyId);
            return await _http.SendAsync(request);
        }

        public async Task<HttpResponseMessage> DeleteAsync(string path, string? agencyId = null, string? serviceToken = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, path);
            AddHeaders(request, agencyId, serviceToken);
            _logger.LogDebug("Gateway DELETE {Path} AgencyId={AgencyId}", path, agencyId);
            return await _http.SendAsync(request);
        }
    }
}