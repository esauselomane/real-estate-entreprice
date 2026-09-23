using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CRM.Microservice.Controllers;
using CRM.Microservice.Data;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Services;
using CRM.Microservice.Services.Implementation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class BrokerSyndicationTests
{
    [Fact]
    public async Task CodesAreScopedToAgencyAndUpdatingOneEnvironmentPreservesOthers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new CRMDbContext(new DbContextOptionsBuilder<CRMDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        database.BrokerRecords.Add(new BrokerRecord { BrokerId = "broker", TenantId = "agency", AgencyId = "agency", FullName = "Broker" });
        await database.SaveChangesAsync();
        var controller = CreateController(database, "agency");
        Assert.IsType<OkObjectResult>(await controller.SaveBrokerSyndicationCodes("broker", new(new()
        {
            ["private-property:Sandbox"] = "SANDBOX-1",
            ["private-property:Production"] = "LIVE-1"
        })));
        Assert.IsType<OkObjectResult>(await controller.SaveBrokerSyndicationCodes("broker", new(new() { ["private-property:Sandbox"] = "SANDBOX-2" })));
        var codes = JsonSerializer.Deserialize<Dictionary<string, string>>((await database.BrokerRecords.SingleAsync()).SyndicationAgentCodesJson)!;
        Assert.Equal("LIVE-1", codes["private-property:Production"]);
        Assert.Equal("SANDBOX-2", codes["private-property:Sandbox"]);
        var otherAgency = CreateController(database, "other");
        Assert.IsType<NotFoundObjectResult>(await otherAgency.GetBrokerSyndicationCodes("broker"));
        Assert.IsType<NotFoundObjectResult>(await otherAgency.SaveBrokerSyndicationCodes("broker", new(new() { ["private-property:Sandbox"] = "WRONG" })));
        Assert.IsType<BadRequestObjectResult>(await controller.SaveBrokerSyndicationCodes("broker", new(new() { ["invalid"] = "code" })));
        Assert.IsType<OkObjectResult>(await controller.SaveBrokerSyndicationCodes("broker", new(new() { ["private-property:Sandbox"] = "" })));
        codes = JsonSerializer.Deserialize<Dictionary<string, string>>((await database.BrokerRecords.SingleAsync()).SyndicationAgentCodesJson)!;
        Assert.False(codes.ContainsKey("private-property:Sandbox"));
        Assert.Equal("LIVE-1", codes["private-property:Production"]);
    }

    private static CRMController CreateController(CRMDbContext database, string tenantId)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identity:BaseUrl"] = "https://identity.test", ["Gateway:BaseUrl"] = "https://gateway.test"
        }).Build();
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "AgencyAdmin"), new Claim("TenantId", tenantId)], "test")) };
        var clients = new BrokerPermissionClientFactory();
        var permissions = new PermissionCheckService(clients, configuration, new HttpContextAccessor { HttpContext = context });
        var agencies = new AgencyRepository(database, NullLogger<Repository<AgencyRecord>>.Instance);
        var brokers = new BrokerRepository(database, NullLogger<Repository<BrokerRecord>>.Instance);
        return new CRMController(database, clients, configuration, NullLogger<CRMController>.Instance, permissions,
            agencies, brokers, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!)
            { ControllerContext = new ControllerContext { HttpContext = context } };
    }
}

internal sealed class BrokerPermissionClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(new BrokerPermissionHandler());
}

internal sealed class BrokerPermissionHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"granted\":true}") });
}
