using System.Security.Claims;
using System.Text.Json;
using Developer.API.Controllers;
using Developer.API.Data;
using Developer.API.DTOs;
using Developer.API.Middleware;
using Developer.API.Models;
using Developer.API.Services.PublicListings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class PublicListingTests
{
    [Fact]
    public async Task SearchAndDetailExposeOnlyApprovedListingsInTheKeyAgency()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var visible = fixture.AddProperty(hot: true);
        var hidden = fixture.AddProperty(published: false);
        var foreign = fixture.AddProperty(agencyId: "AGENCY-B");
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties);
        PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch(), default));
        var page = Assert.IsType<PublicSearchResult>(result.Value);
        Assert.Equal(visible.PropertyId, Assert.Single(page.Items).Id);
        Assert.Equal("owner@example.test", page.Items[0].Owner.Email);
        Assert.DoesNotContain("private-title-deed", JsonSerializer.Serialize(page));
        Assert.IsType<NotFoundResult>(await controller.Detail(hidden.PropertyId, default));
        Assert.IsType<NotFoundResult>(await controller.Detail(foreign.PropertyId, default));
        visible.IsPublicListing = false; await fixture.Properties.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await controller.Detail(visible.PropertyId, default));
    }

    [Fact]
    public async Task HotEndpointExcludesOrdinaryAndUnpublishedProperties()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var hot = fixture.AddProperty(hot: true);
        fixture.AddProperty(); fixture.AddProperty(published: false, hot: true);
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Hot(new PublicPropertySearch(), default));
        Assert.Equal(hot.PropertyId, Assert.Single(Assert.IsType<PublicSearchResult>(result.Value).Items).Id);
    }

    [Fact]
    public async Task SearchIncludesNearbyPropertiesAfterExactAreaAndHonoursRadius()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var exact = fixture.AddProperty(address: "Rosebank, Johannesburg");
        var nearby = fixture.AddProperty(address: "Parktown, Johannesburg", latitude: -26.16m);
        fixture.AddProperty(address: "Cape Town", latitude: -33.9m);
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { Location = "Rosebank", RadiusKm = 5 }, default));
        var page = Assert.IsType<PublicSearchResult>(result.Value);
        Assert.Equal(2, page.Total); Assert.True(page.NearbyAvailable);
        Assert.Equal(exact.PropertyId, page.Items[0].Id);
        Assert.Equal(nearby.PropertyId, page.Items[1].Id); Assert.True(page.Items[1].IsNearby);
        result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { Location = "Rosebank", IncludeNearby = false }, default));
        Assert.Single(Assert.IsType<PublicSearchResult>(result.Value).Items);
    }

    [Fact]
    public async Task NearbyAreaSuggestionsAreSeparateFromExplicitlyIncludedResults()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var exact = fixture.AddProperty(address: "Rosebank, Johannesburg");
        var selected = fixture.AddProperty(address: "Parktown, Johannesburg", latitude: -26.16m);
        fixture.AddProperty(address: "Melville, Johannesburg", latitude: -26.17m);
        fixture.AddProperty(published: false, address: "Hidden suburb, Johannesburg", latitude: -26.15m);
        fixture.AddProperty(agencyId: "AGENCY-B", address: "Foreign suburb, Johannesburg", latitude: -26.15m);
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var search = new PublicPropertySearch { Location = "Rosebank", IncludeNearby = false, RadiusKm = 5 };
        var response = Assert.IsType<OkObjectResult>(await controller.Search(search, default));
        var result = Assert.IsType<PublicSearchResult>(response.Value);
        Assert.Equal(exact.PropertyId, Assert.Single(result.Items).Id);
        Assert.Equal(2, result.NearbyAreas!.Count);
        Assert.Contains(result.NearbyAreas, area => area.Name == "Parktown, Johannesburg");
        search.IncludeNearby = true; search.SelectedNearbyAreas.Add("Parktown, Johannesburg");
        response = Assert.IsType<OkObjectResult>(await controller.Search(search, default));
        result = Assert.IsType<PublicSearchResult>(response.Value);
        Assert.Equal(2, result.Items.Count); Assert.Contains(result.Items, property => property.Id == selected.PropertyId);
        Assert.DoesNotContain(result.Items, property => property.Address.StartsWith("Melville"));
    }

    [Fact]
    public async Task SearchWithoutKnownAreaDoesNotInventNearbyResults()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        fixture.AddProperty(); await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { Location = "Unknown town" }, default));
        var page = Assert.IsType<PublicSearchResult>(result.Value);
        Assert.Empty(page.Items); Assert.False(page.NearbyAvailable);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("invalid", false, false)]
    [InlineData("valid", true, false)]
    [InlineData("valid", false, true)]
    public async Task PublicEndpointsRejectMissingInvalidRevokedOrExpiredKeys(string? suppliedKey, bool revoked, bool expired)
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        fixture.Developer.AgencyApiKeys.Add(new AgencyApiKey
        {
            AgencyId = "AGENCY-A", AgencyName = "Agency", KeyHash = ApiKeyGenerator.HashKey("valid"),
            KeyPrefix = "valid", Name = "Website", IsRevoked = revoked, ExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-1) : null
        });
        await fixture.Developer.SaveChangesAsync();
        var called = false;
        Task Next(HttpContext context) { called = true; return Task.CompletedTask; }
        var middleware = new ApiKeyAuthMiddleware(Next);
        var context = new DefaultHttpContext(); context.Request.Path = "/api/developer/properties";
        context.Response.Body = new MemoryStream();
        if (suppliedKey is not null) context.Request.Headers["X-Api-Key"] = suppliedKey;
        await middleware.InvokeAsync(context, fixture.Developer);
        Assert.False(called); Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyAgencyCannotBeOverriddenByHeadersOrQueryParameters()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        fixture.Developer.AgencyApiKeys.Add(new AgencyApiKey { AgencyId = "AGENCY-A", KeyHash = ApiKeyGenerator.HashKey("valid"), Name = "Website" });
        await fixture.Developer.SaveChangesAsync();
        string? agency = null;
        Task Next(HttpContext context) { agency = context.Items["AgencyId"] as string; return Task.CompletedTask; }
        var context = new DefaultHttpContext(); context.Request.Path = "/api/developer/properties";
        context.Request.Headers["X-Api-Key"] = "valid"; context.Request.Headers["X-Agency-Id"] = "AGENCY-B";
        context.Request.QueryString = new QueryString("?agencyId=AGENCY-B&tenantId=AGENCY-B");
        await new ApiKeyAuthMiddleware(Next).InvokeAsync(context, fixture.Developer);
        Assert.Equal("AGENCY-A", agency);
    }

    [Fact]
    public async Task ConfiguredAgencyMustMatchTheApiKeyAgency()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        fixture.Developer.AgencyApiKeys.Add(new AgencyApiKey { AgencyId = "AGENCY-A", KeyHash = ApiKeyGenerator.HashKey("valid"), Name = "Website" });
        await fixture.Developer.SaveChangesAsync();
        var called = false;
        Task Next(HttpContext context) { called = true; return Task.CompletedTask; }
        var context = new DefaultHttpContext(); context.Request.Path = "/api/developer/submissions";
        context.Request.Headers["X-Api-Key"] = "valid";
        context.Request.Headers["X-Expected-Agency-Id"] = "AGENCY-B";
        context.Response.Body = new MemoryStream();
        await new ApiKeyAuthMiddleware(Next).InvokeAsync(context, fixture.Developer);
        Assert.Equal(403, context.Response.StatusCode); Assert.False(called);
    }

    [Fact]
    public async Task OwnerSubmissionsAreAgencyBoundAndEveryEditRequiresNewApproval()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var owner = await fixture.AddOwnerAsync();
        var sessions = new OwnerSessionService(fixture.Developer);
        var token = await sessions.CreateAsync(owner, default);
        var controller = new OwnerSubmissionsController(fixture.Properties, sessions);
        PublicListingFixture.SetAgency(controller, ownerToken: token);
        var request = new OwnerPropertySubmission
        {
            Address = "An owner property", Description = "New listing", PropertyType = "Residential", ErfSize = "100 m²",
            IsForSale = true, SalePrice = 100000, PublishContactConsent = true
        };
        Assert.IsType<OkObjectResult>(await controller.Create(request, default));
        var property = await fixture.Properties.PropertyAssets.SingleAsync();
        Assert.Equal(owner.AgencyId, property.TenantId); Assert.Equal(owner.Id, property.PublicOwnerAccountId);
        Assert.Equal("Pending review", property.PublicReviewStatus); Assert.False(property.IsPublicListing);
        property.IsPublicListing = true; property.IsHotProperty = true; property.PublicReviewStatus = "Approved";
        await fixture.Properties.SaveChangesAsync();
        request.Description = "Edited by owner";
        Assert.IsType<OkObjectResult>(await controller.Update(property.PropertyId, request, default));
        Assert.False(property.IsPublicListing); Assert.True(property.IsHotProperty); Assert.Equal("Pending review", property.PublicReviewStatus);
        PublicListingFixture.SetAgency(controller, "AGENCY-B", token);
        Assert.IsType<UnauthorizedResult>(await controller.List(default));
        var otherOwner = new PublicOwnerAccount { AgencyId = "AGENCY-A", Email = "OTHER@EXAMPLE.TEST" };
        fixture.Developer.PublicOwnerAccounts.Add(otherOwner); await fixture.Developer.SaveChangesAsync();
        var otherToken = await sessions.CreateAsync(otherOwner, default);
        PublicListingFixture.SetAgency(controller, ownerToken: otherToken);
        Assert.IsType<NotFoundResult>(await controller.Update(property.PropertyId, request, default));
    }

    [Fact]
    public async Task AgencyAdministratorCannotGenerateOrRevokeAnotherAgencysKey()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var key = new AgencyApiKey { AgencyId = "AGENCY-B", KeyHash = "hash", Name = "Other agency" };
        fixture.Developer.AgencyApiKeys.Add(key); await fixture.Developer.SaveChangesAsync();
        var controller = new DeveloperKeysController(fixture.Developer, NullLogger<DeveloperKeysController>.Instance);
        PublicListingFixture.SetAgency(controller);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "AgencyAdmin"), new Claim("TenantId", "AGENCY-A") }, "test"));
        Assert.IsType<ForbidResult>(await controller.GenerateKey(new GenerateApiKeyRequest { AgencyId = "AGENCY-B", Name = "Unauthorised" }));
        Assert.IsType<ForbidResult>(await controller.RevokeKey(key.Id)); Assert.False(key.IsRevoked);
    }

    [Fact]
    public async Task RentalPriceBoundsMustMatchTheSameUnit()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var property = fixture.AddProperty(); property.IsForRent = true;
        property.SubUnits.Add(new Property.Microservice.Models.PropertyUnit { UnitNumber = "Low", UnitDescription = "Low", RentalRate = 100 });
        property.SubUnits.Add(new Property.Microservice.Models.PropertyUnit { UnitNumber = "High", UnitDescription = "High", RentalRate = 1000 });
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { ListingType = "Rent", MinPrice = 500, MaxPrice = 600 }, default));
        Assert.Empty(Assert.IsType<PublicSearchResult>(result.Value).Items);
    }

    [Fact]
    public async Task RandomHotSelectionReturnsFiveDistinctApprovedPropertiesOnly()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        for (var index = 0; index < 12; index++) fixture.AddProperty(hot: true);
        fixture.AddProperty(hot: false); fixture.AddProperty(published: false, hot: true);
        fixture.AddProperty(agencyId: "AGENCY-B", hot: true);
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Hot(new PublicPropertySearch { Randomize = true, PageSize = 5 }, default));
        var page = Assert.IsType<PublicSearchResult>(result.Value);
        Assert.Equal(12, page.Total); Assert.Equal(5, page.Items.Count);
        Assert.Equal(5, page.Items.Select(item => item.Id).Distinct().Count());
        Assert.All(page.Items, AssertHot);
    }

    private static void AssertHot(PublicProperty property) => Assert.True(property.IsHot);

    [Fact]
    public void ThumbnailIsSmallAndDoesNotReplaceTheDetailGallery()
    {
        using var original = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(720, 440);
        using var stream = new MemoryStream();
        SixLabors.ImageSharp.ImageExtensions.SaveAsPng(original, stream);
        var source = "data:image/png;base64," + Convert.ToBase64String(stream.ToArray());
        var property = new Property.Microservice.Models.PropertyAsset { GalleryImageJson = JsonSerializer.Serialize(new[] { source }) };
        var mapped = PublicListingMapper.Map(property);
        Assert.Equal(source, Assert.Single(mapped.Images)); Assert.NotNull(mapped.Thumb);
        var bytes = Convert.FromBase64String(mapped.Thumb[(mapped.Thumb.IndexOf(',') + 1)..]);
        var thumbnail = SixLabors.ImageSharp.Image.Identify(bytes);
        Assert.Equal(150, thumbnail.Width); Assert.Equal(100, thumbnail.Height);
    }

    [Fact]
    public async Task CombinedAndRoomSearchesKeepListingPurposesDistinct()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var saleOnly = fixture.AddProperty();
        var both = fixture.AddProperty(); both.IsForRent = true;
        var room = fixture.AddProperty(); room.IsForSale = false; room.IsForRent = true; room.RoomLettingType = "SharedRoom";
        room.SubUnits.Add(new Property.Microservice.Models.PropertyUnit { UnitNumber = "Room", UnitDescription = "Shared room", RentalRate = 3200 });
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var combined = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { ListingType = "Both" }, default));
        Assert.Equal(both.PropertyId, Assert.Single(Assert.IsType<PublicSearchResult>(combined.Value).Items).Id);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { ListingType = "Room", RoomLettingType = "SharedRoom", MinPrice = 3000, MaxPrice = 3500 }, default));
        var match = Assert.Single(Assert.IsType<PublicSearchResult>(result.Value).Items);
        Assert.Equal(room.PropertyId, match.Id); Assert.Equal("SharedRoom", match.RoomLettingType);
        result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { ListingType = "Room", RoomLettingType = "PrivateRoom" }, default));
        Assert.Empty(Assert.IsType<PublicSearchResult>(result.Value).Items);
    }

    [Fact]
    public async Task RoomSubmissionsRequireExplicitPurposeAndUseRoomPropertyType()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var owner = await fixture.AddOwnerAsync(); var sessions = new OwnerSessionService(fixture.Developer);
        var token = await sessions.CreateAsync(owner, default);
        var controller = new OwnerSubmissionsController(fixture.Properties, sessions); PublicListingFixture.SetAgency(controller, ownerToken: token);
        var request = new OwnerPropertySubmission { Address = "A room", Description = "Shared room", PropertyType = "Residential", ErfSize = "20 m²", RoomLettingType = "SharedRoom", PublishContactConsent = true };
        Assert.IsType<BadRequestObjectResult>(await controller.Create(request, default));
        request.IsForRent = true;
        request.Units.Add(new OwnerUnitSubmission { Number = "Room 1", Description = "Shared room", RentalPrice = 3200 });
        Assert.IsType<OkObjectResult>(await controller.Create(request, default));
        var property = await fixture.Properties.PropertyAssets.SingleAsync();
        Assert.True(property.IsForRent); Assert.False(property.IsForSale);
        Assert.Equal("SharedRoom", property.RoomLettingType); Assert.Equal(2, property.PropertyTypeId); Assert.False(property.IsPublicListing);
    }

    [Fact]
    public async Task SupportedTypeUpgradePreservesExistingTypesAndAddsLandIdempotently()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        Property.Microservice.Services.PublicListings.PublicListingSchema.EnsurePropertyTypes(fixture.Properties);
        Property.Microservice.Services.PublicListings.PublicListingSchema.EnsurePropertyTypes(fixture.Properties);
        Assert.Equal(6, await fixture.Properties.PropertyTypes.CountAsync());
        Assert.Equal("Residential", (await fixture.Properties.PropertyTypes.FindAsync(1))!.Name);
        Assert.Single(await fixture.Properties.PropertyTypes.Where(type => type.Name == "Land").ToListAsync());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml,<svg onload='alert(1)'/>")]
    [InlineData("http://example.test/property.jpg")]
    public void MapperRejectsExecutableAndInsecureImageSources(string source) => Assert.False(PublicListingMapper.IsSafeImage(source));
}
