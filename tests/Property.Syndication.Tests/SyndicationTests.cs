using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Models.Syndication;
using Property.Microservice.Services.Syndication;
using Property.Microservice.DTOs.Syndication;
using Xunit;

namespace Property.Syndication.Tests;

public sealed class SyndicationTests
{
    [Fact]
    public async Task QueueSurvivesNewContextAndIsNotLiveUntilConfirmed()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var publication = await fixture.QueueAsync(true);
        Assert.Equal("Queued", publication.State);
        Assert.False(publication.ConfirmedPublished);
        Assert.Empty(publication.ExternalId);
        await using var restarted = new PropertyDbContext(fixture.Options);
        Assert.True(await new SyndicationProcessor(restarted, fixture.Registry).ProcessNextAsync(default));
        var saved = await restarted.SyndicationPublications.SingleAsync();
        Assert.Equal("Live", saved.State);
        Assert.True(saved.ConfirmedPublished);
        Assert.Equal("external-1", saved.ExternalId);
        Assert.False(await new SyndicationProcessor(restarted, fixture.Registry).ProcessNextAsync(default));
        Assert.Single(fixture.Provider.Operations);
    }

    [Fact]
    public async Task LaterWithdrawalSupersedesUnsentPublish()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        await fixture.QueueAsync(true);
        await fixture.QueueAsync(false);
        fixture.Provider.Result = new ProviderResult("Withdrawn");
        var processor = new SyndicationProcessor(fixture.Database, fixture.Registry);
        await processor.ProcessNextAsync(default);
        Assert.Empty(fixture.Provider.Operations);
        await processor.ProcessNextAsync(default);
        Assert.Equal("Withdraw", Assert.Single(fixture.Provider.Operations));
        Assert.False((await fixture.Database.SyndicationPublications.SingleAsync()).ConfirmedPublished);
    }

    [Fact]
    public async Task SubmittedIsPolledBeforeItIsShownAsLive()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        fixture.Provider.Result = new ProviderResult("Submitted", "external-1");
        await fixture.QueueAsync(true);
        var processor = new SyndicationProcessor(fixture.Database, fixture.Registry);
        await processor.ProcessNextAsync(default);
        var publication = await fixture.Database.SyndicationPublications.SingleAsync();
        Assert.False(publication.ConfirmedPublished);
        Assert.Equal("Submitted", publication.State);
        var command = await fixture.Database.SyndicationCommands.SingleAsync();
        command.AvailableAt = DateTime.UtcNow.AddSeconds(-1);
        await fixture.Database.SaveChangesAsync();
        fixture.Provider.Result = new ProviderResult("Live", "external-1");
        await processor.ProcessNextAsync(default);
        Assert.Equal(new[] { "Publish", "CheckStatus" }, fixture.Provider.Operations);
        Assert.True(publication.ConfirmedPublished);
    }

    [Fact]
    public async Task TransientRetryKeepsRequestKeyAndAmbiguousFailureDoesNotRetry()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        fixture.Provider.Result = new ProviderResult("NeedsAttention", Error: "Rate limited", RetrySafe: true);
        await fixture.QueueAsync(true);
        var processor = new SyndicationProcessor(fixture.Database, fixture.Registry);
        await processor.ProcessNextAsync(default);
        var command = await fixture.Database.SyndicationCommands.SingleAsync();
        Assert.Equal("Pending", command.State);
        command.AvailableAt = DateTime.UtcNow.AddSeconds(-1);
        await fixture.Database.SaveChangesAsync();
        fixture.Provider.Throw = true;
        await processor.ProcessNextAsync(default);
        Assert.Equal(fixture.Provider.RequestKeys[0], fixture.Provider.RequestKeys[1]);
        Assert.Equal("NeedsAttention", command.State);
        Assert.DoesNotContain("sensitive", command.Error);
        Assert.False(await processor.ProcessNextAsync(default));
    }

    [Fact]
    public async Task CrossAgencyConnectionIsRejectedWithoutQueueing()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        fixture.Connection.TenantId = "other-agency";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.QueueAsync(true));
        Assert.Empty(fixture.Database.SyndicationCommands);
    }

    [Fact]
    public async Task SecondProviderDoesNotRequireWorkflowChanges()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var second = new TestListingProvider("second");
        var registry = new ListingProviderRegistry([fixture.Provider, second]);
        fixture.Connection.ProviderKey = "second";
        await fixture.Database.SaveChangesAsync();
        await new SyndicationCoordinator(fixture.Database, registry).QueueAsync(fixture.Property, fixture.Connection, true, default);
        await new SyndicationProcessor(fixture.Database, registry).ProcessNextAsync(default);
        Assert.Single(second.Operations);
        Assert.Empty(fixture.Provider.Operations);
    }

    [Fact]
    public async Task UncertainOutcomeRequiresStatusCheckBeforeAnotherPublish()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        await fixture.QueueAsync(true);
        fixture.Provider.Throw = true;
        var processor = new SyndicationProcessor(fixture.Database, fixture.Registry);
        await processor.ProcessNextAsync(default);
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.QueueAsync(true));
        fixture.Provider.Throw = false;
        fixture.Provider.Result = new ProviderResult("NotPublished");
        await new SyndicationCoordinator(fixture.Database, fixture.Registry).QueueAsync(fixture.Property, fixture.Connection, false, default, reconcile: true);
        await processor.ProcessNextAsync(default);
        Assert.Equal("CheckStatus", fixture.Provider.Operations.Last());
        var publication = await fixture.QueueAsync(true);
        Assert.Equal("Queued", publication.State);
    }

    [Fact]
    public async Task FailedWithdrawalPreservesLastConfirmedLiveStatus()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        await fixture.QueueAsync(true);
        var processor = new SyndicationProcessor(fixture.Database, fixture.Registry);
        await processor.ProcessNextAsync(default);
        await fixture.QueueAsync(false);
        fixture.Provider.Throw = true;
        await processor.ProcessNextAsync(default);
        var publication = await fixture.Database.SyndicationPublications.SingleAsync();
        Assert.True(publication.ConfirmedPublished);
        Assert.False(publication.DesiredPublished);
        Assert.Equal("NeedsAttention", publication.State);
    }

    [Fact]
    public async Task StatusEndpointDoesNotExposeAnotherAgencysConnections()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var controller = new Property.Microservice.Controllers.SyndicationController(fixture.Database, null!, null!, null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Property.Microservice.Controllers.SyndicationController>.Instance,
            fixture.Registry, new SyndicationCoordinator(fixture.Database, fixture.Registry));
        var claims = new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Broker"),
            new System.Security.Claims.Claim("TenantId", "other-agency")], "test");
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(claims) }
        };
        var result = await controller.GetStatus(fixture.Property.PropertyId, default);
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(result);
    }

    [Fact]
    public void CredentialsAreResolvedSeparatelyForEachConnection()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Syndication:ConnectionCredentials:1:ApiKey"] = "agency-one-key",
                ["Syndication:ConnectionCredentials:2:ApiKey"] = "agency-two-key"
            }).Build();
        var source = new ConfigurationProviderCredentialSource(configuration);
        Assert.Equal("agency-one-key", source.Resolve(new SyndicationConnection { Id = 1 })["ApiKey"]);
        Assert.Equal("agency-two-key", source.Resolve(new SyndicationConnection { Id = 2 })["ApiKey"]);
        Assert.Throws<InvalidOperationException>(() => source.Resolve(new SyndicationConnection { Id = 3 }));
    }

    [Fact]
    public void DeployedProviderAssemblyIsDiscoveredFromConfiguration()
    {
        var assembly = typeof(TestListingProvider).Assembly;
        var name = assembly.GetName().Name!;
        var directory = Path.Combine(AppContext.BaseDirectory, "providers", name);
        Directory.CreateDirectory(directory);
        File.Copy(assembly.Location, Path.Combine(directory, name + ".dll"), overwrite: true);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Syndication:ProviderAssemblies:0"] = name
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddListingProviders(configuration);
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ListingProviderRegistry>();
        Assert.Equal("test-provider", registry.Get("test-provider").Descriptor.Key);
        Assert.False(registry.Get("private-property").Descriptor.Ready);
    }

    [Fact]
    public async Task PrivatePropertyIsBlockedUntilAuthenticationContractIsAvailable()
    {
        await using var fixture = await SyndicationFixture.CreateAsync();
        var provider = new PrivatePropertyProvider();
        fixture.Connection.ProviderKey = provider.Descriptor.Key;
        var coordinator = new SyndicationCoordinator(fixture.Database, new ListingProviderRegistry([provider]));
        var error = await Assert.ThrowsAsync<ArgumentException>(() => coordinator.QueueAsync(fixture.Property, fixture.Connection, true, default));
        Assert.Contains("authentication guide", error.Message);
        Assert.Empty(fixture.Database.SyndicationPublications);
        Assert.Empty(fixture.Database.SyndicationCommands);
    }
}
