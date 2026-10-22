using Developer.API.Data;
using Developer.API.Models;
using Identity.Microservice.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Property.Microservice.Data;
using Property.Microservice.Models;
using Property.Microservice.Services.PublicListings;
using System.Text.Json;

var repositoryRoot = Path.GetFullPath(args.FirstOrDefault() ?? Directory.GetCurrentDirectory());
var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(repositoryRoot, "Developer.API"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.local.json", optional: true).AddEnvironmentVariables().Build();
var developerConnection = RequireLocalConnection(configuration.GetConnectionString("DeveloperConnection"));
var propertyConnection = RequireLocalConnection(configuration.GetConnectionString("PropertyConnection"));
var identityConnection = new SqlConnectionStringBuilder(developerConnection) { InitialCatalog = "RealEstateIdentityDB" }.ConnectionString;
await using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(identityConnection).Options);
var administrator = await identity.UserAccounts.AsNoTracking().FirstOrDefaultAsync(user => user.UserId == "USR-ADMIN" && user.SystemRole == "SuperAdmin")
    ?? throw new InvalidOperationException("The seeded SuperAdmin account was not found. Start Identity locally first.");
var agency = await identity.TenantAgencies.AsNoTracking().SingleAsync(item => item.TenantId == administrator.TenantId);
if (agency.TenantId != "TEN-001") throw new InvalidOperationException("SuperAdmin's agency changed. Review the test scenario before seeding another agency.");
await using var properties = new PropertyDbContext(new DbContextOptionsBuilder<PropertyDbContext>().UseSqlServer(propertyConnection).Options);
await using var developer = new DeveloperDbContext(new DbContextOptionsBuilder<DeveloperDbContext>().UseSqlServer(developerConnection).Options);
await properties.Database.EnsureCreatedAsync();
PublicListingSchema.EnsureCreated(properties);
PublicListingSchema.EnsurePropertyTypes(properties);
await developer.Database.EnsureCreatedAsync();
PublicOwnerSchema.EnsureCreated(developer);
const string ownerId = "sample-public-e2e-ten-001";
const string ownerEmail = "OWNER.PUBLIC-E2E@EXAMPLE.TEST";
var owner = await developer.PublicOwnerAccounts.FindAsync(ownerId);
if (owner is null)
{
    owner = new PublicOwnerAccount { Id = ownerId, AgencyId = agency.TenantId, Email = ownerEmail, Name = "SAMPLE E2E Owner", Phone = "+27000000000" };
    owner.PasswordHash = new PasswordHasher<PublicOwnerAccount>().HashPassword(owner, "BiscoveSampleOwner2026!");
    developer.PublicOwnerAccounts.Add(owner);
    await developer.SaveChangesAsync();
}
var definitions = SamplePropertyDefinition.CreateScenario();
if (args.Contains("--repair-samples"))
{
    // Explicit recovery for the legacy startup backfill; touches only this owner's labelled fixtures.
    foreach (var definition in definitions)
    {
        var marker = $"[PUBLIC-SITE-E2E:{definition.Code}]";
        var sample = await properties.PropertyAssets.FirstOrDefaultAsync(item => item.PublicOwnerAccountId == ownerId && item.PropertyDescription.Contains(marker));
        if (sample is null) continue;
        sample.TenantId = agency.TenantId;
        sample.IsForSale = definition.SalePrice.HasValue;
        sample.IsForRent = definition.RentalPrice.HasValue;
        sample.Latitude = definition.Latitude;
        sample.Longitude = definition.Longitude;
        sample.IsPublicListing = definition.Status == "Approved";
        sample.IsHotProperty = definition.Hot;
        sample.PublicReviewStatus = definition.Status;
    }
    await properties.SaveChangesAsync();
}
var created = 0;
foreach (var definition in definitions)
{
    var marker = $"[PUBLIC-SITE-E2E:{definition.Code}]";
    var exists = await properties.PropertyAssets.AnyAsync(item => item.TenantId == agency.TenantId && item.PropertyDescription.Contains(marker));
    if (exists) continue;
    var propertyType = await properties.PropertyTypes.SingleAsync(item => item.Name == (definition.RoomLettingType != "None" ? "Room share/let" : definition.Type));
    var images = SampleImages.Create(definition.Type, definition.ImageTheme);
    var property = new PropertyAsset
    {
        TenantId = agency.TenantId, CompleteAddress = "SAMPLE E2E — " + definition.Address,
        Location = await Property.Microservice.Services.Locations.PropertyLocationResolver.ResolveAsync(properties, SampleLocations.FromFixtureAddress(definition.Address)),
        ErfSpatialSize = definition.Size + " m²", TitleDeedType = "Sample record", PortfolioZoning = definition.Type,
        RoomLettingType = definition.RoomLettingType, PropertyTypeId = propertyType.PropertyTypeId, IsForSale = definition.SalePrice.HasValue, IsForRent = definition.RentalPrice.HasValue,
        MarketClass = definition.SalePrice.HasValue ? "Sale" : "Rental", SalePrice = definition.SalePrice,
        Latitude = definition.Latitude, Longitude = definition.Longitude,
        PropertyDescription = $"{marker} Fictional {definition.Type.ToLowerInvariant()} property for local end-to-end testing only. Schematic sample images and approximate locations; not a real listing. Includes two units so you can test availability, individual prices and galleries.",
        LinkedOwnerId = "public:" + ownerId, PublicOwnerAccountId = ownerId, PublicOwnerName = owner.Name,
        PublicOwnerEmail = owner.Email, PublicOwnerPhone = owner.Phone, GalleryImageJson = JsonSerializer.Serialize(images),
        IsPublicListing = definition.Status == "Approved", IsHotProperty = definition.Hot,
        PublicReviewStatus = definition.Status, PublicSubmittedAt = DateTime.UtcNow
    };
    for (var index = 1; index <= 2; index++)
    {
        property.SubUnits.Add(new PropertyUnit
        {
            UnitNumber = "Sample unit " + index, UnitDescription = "Fictional " + definition.Type.ToLowerInvariant() + " unit " + index,
            NetRentableAreaSqM = definition.Size / 2m, RentalRate = definition.RentalPrice,
            SpecificUnitRentalPrice = definition.RentalPrice, SpecificUnitSalePrice = definition.SalePrice / 2,
            IsCurrentlyVacant = index == 1, UnitImagesJson = JsonSerializer.Serialize(images.Take(2))
        });
    }
    properties.PropertyAssets.Add(property); created++;
}
await properties.SaveChangesAsync();
if (args.Contains("--confirm-sample-locations")) await SampleLocations.ConfirmExistingFixturesAsync(properties, ownerId, agency.TenantId);
if (args.Contains("--seed-public-features")) await SamplePublicFeatures.SeedAsync(properties, ownerId, agency.TenantId);
var environmentPath = Path.Combine(repositoryRoot, "property-public-site", ".env");
var environment = LocalEnvironment.Read(environmentPath);
var rawKey = environment.GetValueOrDefault("AGENCY_API_KEY", "");
var hash = ApiKeyGenerator.HashKey(rawKey);
var keyIsValid = !string.IsNullOrWhiteSpace(rawKey) && await developer.AgencyApiKeys.AnyAsync(item => item.KeyHash == hash && item.AgencyId == agency.TenantId && !item.IsRevoked && (item.ExpiresAt == null || item.ExpiresAt > DateTime.UtcNow));
if (!keyIsValid)
{
    rawKey = ApiKeyGenerator.GenerateKey();
    developer.AgencyApiKeys.Add(new AgencyApiKey
    {
        AgencyId = agency.TenantId, AgencyName = agency.AgencyName, Name = "Local public website E2E",
        Description = "Local test website key generated by the sample scenario tool.", KeyHash = ApiKeyGenerator.HashKey(rawKey),
        KeyPrefix = ApiKeyGenerator.GetPrefix(rawKey), CreatedBy = administrator.UserId
    });
    await developer.SaveChangesAsync();
}
environment["AGENCY_ID"] = agency.TenantId;
environment["AGENCY_DISPLAY_NAME"] = agency.AgencyName;
environment["AGENCY_API_KEY"] = rawKey;
environment.TryAdd("PORT", "3010");
environment.TryAdd("PUBLIC_SITE_ORIGIN", "http://localhost:3010");
environment.TryAdd("DEVELOPER_API_URL", "http://localhost:5006/api/developer/");
environment.TryAdd("NODE_ENV", "development");
LocalEnvironment.Write(environmentPath, environment);
var samples = await properties.PropertyAssets.AsNoTracking().Where(item => item.TenantId == agency.TenantId && item.PropertyDescription.Contains("[PUBLIC-SITE-E2E:"))
    .OrderBy(item => item.PropertyId).Select(item => new { item.PropertyId, item.CompleteAddress, item.PublicReviewStatus, item.IsHotProperty }).ToArrayAsync();
Console.WriteLine($"Agency verified from SuperAdmin: {agency.TenantId} ({agency.AgencyName}). Created {created}; total sample properties: {samples.Length}.");
Console.WriteLine("Sample owner: owner.public-e2e@example.test (credentials documented in property-public-site/SAMPLE_DATA.md).");
Console.WriteLine("Agency key saved to ignored website .env; not printed.");
Console.WriteLine(JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));

static string RequireLocalConnection(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("Configure local Developer and Property connection strings first.");
    var builder = new SqlConnectionStringBuilder(connectionString);
    var host = builder.DataSource.Split(',')[0];
    if (host is not ("localhost" or "127.0.0.1" or ".")) throw new InvalidOperationException("The sample seeder only permits loopback SQL Server connections.");
    return builder.ConnectionString;
}
