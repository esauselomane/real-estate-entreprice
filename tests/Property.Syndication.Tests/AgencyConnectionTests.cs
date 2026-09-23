using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Property.Microservice.Controllers;
using Property.Microservice.DTOs.Syndication;
using Property.Microservice.Services;
using Property.Microservice.Services.Syndication;
using Xunit;

namespace Property.Syndication.Tests;

public sealed class AgencyConnectionTests
{
    [Fact]
    public async Task SavingReplacingAndRemovingKeyNeverReturnsSecretAndBlankPreservesIt()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var configuration = Configuration();
        var protector = new AgencyApiKeyProtector(new EphemeralDataProtectionProvider(), configuration, new TestHostEnvironment());
        var controller = CreateController(fixture, configuration, "agency");
        var request = new AgencyConnectionRequest("Sandbox", "branch", "first-secret");
        var response = Assert.IsType<OkObjectResult>(await controller.SaveAgencyConnection("agency", "test-provider", request, protector, default));
        Assert.DoesNotContain("first-secret", JsonSerializer.Serialize(response.Value));
        Assert.DoesNotContain("ProtectedApiKey", JsonSerializer.Serialize(response.Value));
        Assert.Equal("first-secret", protector.Unprotect(fixture.Connection));
        Assert.NotEqual("first-secret", fixture.Connection.ProtectedApiKey);
        await controller.SaveAgencyConnection("agency", "test-provider", request with { ApiKey = " " }, protector, default);
        Assert.Equal("first-secret", protector.Unprotect(fixture.Connection));
        await controller.SaveAgencyConnection("agency", "test-provider", request with { ApiKey = "replacement" }, protector, default);
        Assert.Equal("replacement", protector.Unprotect(fixture.Connection));
        await controller.SaveAgencyConnection("agency", "test-provider", request with { ApiKey = null, RemoveApiKey = true }, protector, default);
        Assert.False(fixture.Connection.HasApiKey);
        Assert.False(fixture.Connection.Enabled);
    }

    [Fact]
    public async Task DifferentAgencyCannotReadOrWriteConnection()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var controller = CreateController(fixture, Configuration(), "another-agency");
        Assert.IsType<ForbidResult>(await controller.GetAgencyConnections("agency", default));
        Assert.IsType<ForbidResult>(await controller.SaveAgencyConnection("agency", "test-provider", new("Sandbox", "", "secret"), null!, default));
        Assert.Equal("test-only", fixture.Connection.ProtectedApiKey);
    }

    [Fact]
    public async Task EncryptedKeyCannotBeMovedToAnotherTenant()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var protector = new AgencyApiKeyProtector(new EphemeralDataProtectionProvider(), Configuration(), new TestHostEnvironment());
        fixture.Connection.ProtectedApiKey = protector.Protect(fixture.Connection, "secret");
        fixture.Connection.TenantId = "other-agency";
        Assert.Throws<CryptographicException>(() => protector.Unprotect(fixture.Connection));
    }

    [Fact]
    public async Task MissingKeyBlocksQueueAndRemovingKeyBlocksAlreadyQueuedPublish()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        await fixture.QueueAsync(true);
        fixture.Connection.ProtectedApiKey = "";
        await fixture.Database.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.QueueAsync(true));
        await new SyndicationProcessor(fixture.Database, fixture.Registry).ProcessNextAsync(default);
        Assert.Empty(fixture.Provider.Operations);
    }

    [Fact]
    public async Task ProductionRequiresPersistentKeyStorage()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var protector = new AgencyApiKeyProtector(new EphemeralDataProtectionProvider(), Configuration(), new TestHostEnvironment { EnvironmentName = "Production" });
        Assert.Throws<InvalidOperationException>(() => protector.Protect(fixture.Connection, "secret"));
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:BaseUrl"] = "https://identity.test" }).Build();

    private static SyndicationController CreateController(SyndicationFixture fixture, IConfiguration configuration, string tenantId)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "AgencyAdmin"), new Claim("TenantId", tenantId)], "test")) };
        var permissions = new PermissionCheckService(new GrantedPermissionClientFactory(), configuration, new HttpContextAccessor { HttpContext = context });
        return new SyndicationController(fixture.Database, permissions, null!, null!, NullLogger<SyndicationController>.Instance,
            fixture.Registry, new SyndicationCoordinator(fixture.Database, fixture.Registry))
            { ControllerContext = new ControllerContext { HttpContext = context } };
    }
}

internal sealed class GrantedPermissionClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new GrantedPermissionHandler());
}

internal sealed class GrantedPermissionHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"granted\":true}") });
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
