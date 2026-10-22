using Developer.API.Data;
using Developer.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.Models;

namespace Developer.PublicListings.Tests;

public sealed class PublicListingFixture : IAsyncDisposable
{
    private readonly SqliteConnection propertyConnection = new("Data Source=:memory:");
    private readonly SqliteConnection developerConnection = new("Data Source=:memory:");
    public PropertyDbContext Properties { get; private set; } = null!;
    public DeveloperDbContext Developer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await propertyConnection.OpenAsync();
        await developerConnection.OpenAsync();
        Properties = new PropertyDbContext(new DbContextOptionsBuilder<PropertyDbContext>().UseSqlite(propertyConnection).Options);
        Developer = new DeveloperDbContext(new DbContextOptionsBuilder<DeveloperDbContext>().UseSqlite(developerConnection).Options);
        await Properties.Database.EnsureCreatedAsync();
        await Developer.Database.EnsureCreatedAsync();
        Properties.PropertyTypes.Add(new PropertyType { PropertyTypeId = 1, Name = "Residential" });
        Properties.PropertyTypes.Add(new PropertyType { PropertyTypeId = 2, Name = "Room share/let" });
        await Properties.SaveChangesAsync();
    }

    public static void SetAgency(ControllerBase controller, string agencyId = "AGENCY-A", string? ownerToken = null)
    {
        var context = new DefaultHttpContext();
        context.Items["AgencyId"] = agencyId;
        if (ownerToken is not null) context.Request.Headers["X-Owner-Token"] = ownerToken;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
    }

    public PropertyAsset AddProperty(string agencyId = "AGENCY-A", bool published = true, bool hot = false, string address = "Rosebank, Johannesburg", decimal latitude = -26.146m)
    {
        var property = new PropertyAsset
        {
            TenantId = agencyId, PropertyTypeId = 1, CompleteAddress = address,
            IsPublicListing = published, PublicReviewStatus = published ? "Approved" : "Pending review", IsHotProperty = hot,
            PropertyDescription = "A home", ErfSpatialSize = "300 m²", Latitude = latitude, Longitude = 28.04m,
            PublicOwnerName = "Owner Example", PublicOwnerEmail = "owner@example.test", PublicOwnerPhone = "+27821234567",
            VerificationDocumentsJson = "[\"private-title-deed.pdf\"]"
        };
        Properties.PropertyAssets.Add(property);
        return property;
    }

    public async Task<PublicOwnerAccount> AddOwnerAsync(string agencyId = "AGENCY-A")
    {
        var owner = new PublicOwnerAccount { AgencyId = agencyId, Email = "OWNER@EXAMPLE.TEST", Name = "Owner Example", Phone = "+27821234567", PasswordHash = "unused-test-hash" };
        Developer.PublicOwnerAccounts.Add(owner);
        await Developer.SaveChangesAsync();
        return owner;
    }

    public async ValueTask DisposeAsync()
    {
        await Properties.DisposeAsync();
        await Developer.DisposeAsync();
        await propertyConnection.DisposeAsync();
        await developerConnection.DisposeAsync();
    }
}
