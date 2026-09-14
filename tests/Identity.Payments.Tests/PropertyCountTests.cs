using System.Net;
using System.Text.Json;
using CRM.Microservice.Services.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class PropertyCountTests
{
    [Fact]
    public async Task GrowthPreservesCumulativeSnapshotsWithoutAddingThemAgain()
    {
        var now = DateTime.UtcNow;
        var previousMonth = now.AddMonths(-1);
        var payload = JsonSerializer.Serialize(new[] {
            new { year = previousMonth.Year, month = previousMonth.Month, propertyCount = 7 },
            new { year = now.Year, month = now.Month, propertyCount = 9 }
        });
        var service = CreateService(payload, HttpStatusCode.OK);
        var points = await service.GetPropertiesGrowthOver6MonthsAsync(["tenant"], new DefaultHttpContext());
        Assert.Equal(6, points.Count);
        Assert.Equal(now.ToString("yyyy-MM"), points[^1].Month);
        Assert.Equal(7, points[^2].PropertyCount);
        Assert.Equal(9, points[^1].PropertyCount);
    }

    [Fact]
    public async Task FailedPropertyLookupIsNotReportedAsZero()
    {
        var service = CreateService("{}", HttpStatusCode.ServiceUnavailable);
        Assert.Null(await service.GetTotalSoleMandatesAsync(["tenant"], new DefaultHttpContext()));
        Assert.Empty(await service.GetPropertiesGrowthOver6MonthsAsync(["tenant"], new DefaultHttpContext()));
    }

    private static PropertyCountService CreateService(string payload, HttpStatusCode status)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:BaseUrl"] = "https://api.example/gateway" }).Build();
        return new PropertyCountService(new Clients(payload, status), configuration, NullLogger<PropertyCountService>.Instance);
    }
    private sealed class Clients(string payload, HttpStatusCode status) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(payload, status));
    }
    private sealed class Handler(string payload, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.StartsWith("/gateway/properties/stats/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload) });
        }
    }
}
