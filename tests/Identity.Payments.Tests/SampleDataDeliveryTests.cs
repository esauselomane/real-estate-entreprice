using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using Identity.Microservice.Services;
using Identity.Microservice.Services.SampleData;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Access.SampleData;
using Shared.Presentation.Email;
using Xunit;

namespace Identity.Payments.Tests;

public sealed class SampleDataDeliveryTests
{
    [Fact]
    public async Task EmailFailureRetriesOnlyNotificationAfterDataHasBeenProvisioned()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        var plan = new SampleDataPlan("trial", "My Agency", "admin@example.invalid", "Sandton", -26, 28, DateTime.UtcNow);
        database.TenantAgencies.Add(new TenantAgency { TenantId = "trial", AgencyName = plan.AgencyName, ContactEmail = plan.AgencyEmail });
        var job = new SampleDataJob { TenantId = "trial", State = "Processing", ApprovedBy = "admin", PlanJson = JsonSerializer.Serialize(plan) };
        database.SampleDataJobs.Add(job);
        await database.SaveChangesAsync();
        var handler = new SampleServiceHandler();
        var delivery = new SampleEmailDelivery(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["SampleData:PropertyServiceUrl"] = "https://property.test", ["SampleData:CrmServiceUrl"] = "https://crm.test",
            ["Urls:FrontendClient"] = "https://app.test" }).Build();
        var processor = new SampleDataProcessor(database, new SampleClientFactory(handler), new TokenService(), configuration,
            new SampleHostEnvironment(), Options.Create(new EmailSettings { FromAddress = "noreply@example.invalid" }), delivery);
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync(job, default));
        Assert.NotNull(job.ProvisionedAt);
        Assert.Null(job.NotifiedAt);
        Assert.Equal(2, handler.Calls);
        delivery.ShouldFail = false;
        await processor.ProcessAsync(job, default);
        Assert.Equal(2, handler.Calls);
        Assert.Equal("Completed", job.State);
        Assert.NotNull(job.NotifiedAt);
        Assert.Equal(5, await database.UserAccounts.CountAsync());
        Assert.All(await database.UserAccounts.ToListAsync(), AssertBrokerCannotSignIn);
        Assert.Equal("https://app.test/", delivery.LastContent!.ActionUrl);
        Assert.Equal(plan.AgencyEmail, delivery.Recipient);
    }

    [Fact]
    public async Task WorkerLeavesUnapprovedRequestsUntouched()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(connection));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await database.Database.EnsureCreatedAsync();
        database.SampleDataJobs.Add(new SampleDataJob { TenantId = "requested" });
        await database.SaveChangesAsync();
        var worker = new SampleDataWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<SampleDataWorker>.Instance);
        await worker.RunNextAsync(default);
        var job = await database.SampleDataJobs.AsNoTracking().SingleAsync();
        Assert.Equal("Requested", job.State);
        Assert.Equal(0, job.Attempts);
    }

    private static void AssertBrokerCannotSignIn(UserAccount account)
    {
        Assert.Equal("De-registered", account.AccountStatus);
        Assert.Empty(account.PasswordHash);
        Assert.Empty(account.ActivationToken);
    }
}

internal sealed class SampleClientFactory(SampleServiceHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

internal sealed class SampleServiceHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Calls++;
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content = JsonContent.Create(new SampleDataResult([new(0, 1, "Sample home", 1500000)]));
        return Task.FromResult(response);
    }
}

internal sealed class SampleEmailDelivery(SampleServiceHandler handler) : IEmailDelivery
{
    public bool ShouldFail { get; set; } = true;
    public EmailContent? LastContent { get; private set; }
    public string? Recipient { get; private set; }
    public Task SendAsync(string fromAddress, string recipient, EmailContent content, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        Assert.Equal(2, handler.Calls);
        if (ShouldFail) throw new HttpRequestException("Email unavailable");
        LastContent = content;
        Recipient = recipient;
        return Task.CompletedTask;
    }
}

internal sealed class SampleHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "SampleTests";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
