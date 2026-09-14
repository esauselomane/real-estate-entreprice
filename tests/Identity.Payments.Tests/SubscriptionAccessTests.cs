using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Access.Subscriptions;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class SubscriptionAccessTests
{
    [Theory]
    [InlineData("{\"requiresUpgrade\":true}", 402, false)]
    [InlineData("{\"requiresUpgrade\":false}", 200, true)]
    [InlineData("{}", 503, false)]
    [InlineData("invalid json", 503, false)]
    public async Task BusinessRequestsRequireVerifiedEntitlement(string payload, int expectedStatus, bool expectedNext)
    {
        var reachedEndpoint = false;
        Task Next(HttpContext context)
        {
            reachedEndpoint = true;
            return Task.CompletedTask;
        }
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Broker")], "test"));
        context.SetEndpoint(new Endpoint(Next, new EndpointMetadataCollection(), "business"));
        context.Response.Body = new MemoryStream();
        context.Request.Headers.Authorization = "Bearer test-token";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:BaseUrl"] = "https://identity.example" }).Build();
        await new SubscriptionAccessMiddleware(Next).InvokeAsync(context, new FakeClients(payload), configuration, NullLogger<SubscriptionAccessMiddleware>.Instance);
        Assert.Equal(expectedNext, reachedEndpoint);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
    }

    private sealed class FakeClients(string payload) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new ResponseHandler(payload));
    }
    private sealed class ResponseHandler(string payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) });
        }
    }
}
