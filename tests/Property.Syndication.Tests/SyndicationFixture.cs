using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Models.Syndication;
using Property.Microservice.Services.Syndication;

namespace Property.Syndication.Tests;

internal sealed class SyndicationFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public PropertyDbContext Database { get; private set; } = null!;
    public DbContextOptions<PropertyDbContext> Options { get; private set; } = null!;
    public TestListingProvider Provider { get; } = new();
    public ListingProviderRegistry Registry => new([Provider]);
    public PropertyAsset Property { get; private set; } = null!;
    public SyndicationConnection Connection { get; private set; } = null!;
    public static async Task<SyndicationFixture> CreateAsync()
    {
        var fixture = new SyndicationFixture();
        await fixture.connection.OpenAsync();
        fixture.Options = new DbContextOptionsBuilder<PropertyDbContext>().UseSqlite(fixture.connection).Options;
        fixture.Database = new PropertyDbContext(fixture.Options);
        await fixture.Database.Database.EnsureCreatedAsync();
        var type = new PropertyType { Name = "Residential" };
        fixture.Database.PropertyTypes.Add(type);
        await fixture.Database.SaveChangesAsync();
        fixture.Property = new PropertyAsset { TenantId = "agency", PropertyTypeId = type.PropertyTypeId, CompleteAddress = "Test address", PropertyDescription = "Test listing", SalePrice = 1000000 };
        fixture.Connection = new SyndicationConnection { TenantId = "agency", ProviderKey = "test-provider", Enabled = true, ProtectedApiKey = "test-only" };
        fixture.Database.AddRange(fixture.Property, fixture.Connection);
        await fixture.Database.SaveChangesAsync();
        return fixture;
    }
    public Task<SyndicationPublication> QueueAsync(bool publish) => new SyndicationCoordinator(Database, Registry).QueueAsync(Property, Connection, publish, default);
    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        await connection.DisposeAsync();
    }
}
