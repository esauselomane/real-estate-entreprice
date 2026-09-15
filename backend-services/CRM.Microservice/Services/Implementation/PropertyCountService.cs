using System.Net.Http.Headers;
using System.Text.Json;
using CRM.Microservice.DTOs;
using CRM.Microservice.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CRM.Microservice.Services.Implementation;

/// <summary>
/// Service implementation that queries the Property microservice via HTTP
/// to obtain property-level counts not stored in the CRM database.
/// </summary>
public class PropertyCountService : IPropertyCountService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PropertyCountService> _logger;

    public PropertyCountService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PropertyCountService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int?> GetTotalSoleMandatesAsync(IReadOnlyList<string> tenantIds, HttpContext httpContext)
    {
        if (tenantIds == null || tenantIds.Count == 0)
            return 0;

        var propertyBaseUrl = _configuration["Gateway:BaseUrl"]
            ?? throw new InvalidOperationException("Gateway:BaseUrl is not configured in appsettings.json.");

        using var http = _httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(propertyBaseUrl.TrimEnd('/') + "/");
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
        }

        var tenantParams = string.Join(",", tenantIds);
        var url = $"properties/stats/sole-mandates?tenantId={Uri.EscapeDataString(tenantParams)}";

        try
        {
            var response = await http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("totalSoleMandates", out var val))
                    return val.GetInt32();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetTotalSoleMandatesAsync: could not fetch sole mandate count from property service");
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PropertyGrowthPoint>> GetPropertiesGrowthOver6MonthsAsync(IReadOnlyList<string> tenantIds, HttpContext httpContext)
    {
        if (tenantIds == null || tenantIds.Count == 0)
            return Array.Empty<PropertyGrowthPoint>();

        var propertyBaseUrl = _configuration["Gateway:BaseUrl"]
            ?? throw new InvalidOperationException("Gateway:BaseUrl is not configured in appsettings.json.");

        using var http = _httpClientFactory.CreateClient();
        http.BaseAddress = new Uri(propertyBaseUrl.TrimEnd('/') + "/");
        var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
        {
            http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authHeader);
        }

        var tenantParams = string.Join(",", tenantIds);
        var url = $"properties/stats/growth?months=6&tenantId={Uri.EscapeDataString(tenantParams)}";

        try
        {
            var response = await http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var points = JsonSerializer.Deserialize<List<PropertyGrowthBucket>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return points == null ? [] : BuildGrowthPoints(points);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetPropertiesGrowthOver6MonthsAsync: could not fetch property growth data from property service");
        }

        return [];
    }

    private static IReadOnlyList<PropertyGrowthPoint> BuildGrowthPoints(IReadOnlyList<PropertyGrowthBucket> buckets)
    {
        var now = DateTime.UtcNow;
        var points = new List<PropertyGrowthPoint>();
        for (int i = 5; i >= 0; i--)
        {
            var month = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            points.Add(new PropertyGrowthPoint
            {
                Month = month.ToString("yyyy-MM"),
                PropertyCount = buckets.Where(bucket => bucket.Year == month.Year && bucket.Month == month.Month).Sum(bucket => bucket.PropertyCount)
            });
        }
        return points;
    }
}
