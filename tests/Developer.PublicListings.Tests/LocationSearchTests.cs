using Developer.API.Controllers;
using Developer.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Property.Microservice.Models;
using Property.Microservice.Services.Locations;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class LocationSearchTests
{
    [Fact]
    public async Task SelectedRosebankDoesNotIncludeItsNamesakeInAnotherProvince()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var johannesburg = fixture.AddProperty(address: "1 Main Road, Rosebank, Johannesburg");
        johannesburg.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, Rosebank("Gauteng", "Johannesburg"));
        var capeTown = fixture.AddProperty(address: "1 Main Road, Rosebank, Cape Town", latitude: -33.95m);
        capeTown.Longitude = 18.47m;
        capeTown.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, Rosebank("Western Cape", "Cape Town"));
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { Location = "Rosebank", LocationId = johannesburg.Location!.LocationId, IncludeNearby = false }, default));
        Assert.Equal(johannesburg.PropertyId, Assert.Single(Assert.IsType<PublicSearchResult>(result.Value).Items).Id);
        Assert.IsType<ConflictObjectResult>(await controller.Search(new PublicPropertySearch { Location = "Rosebank" }, default));
    }

    [Fact]
    public async Task LocationCatalogueContainsOnlyApprovedAgencyInventory()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var own = fixture.AddProperty(); own.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, Rosebank("Gauteng", "Johannesburg"));
        var foreign = fixture.AddProperty(agencyId: "AGENCY-B"); foreign.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, Rosebank("Western Cape", "Cape Town"));
        var hidden = fixture.AddProperty(published: false); hidden.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, Rosebank("Hidden", "Hidden"));
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Locations("Rosebank", default));
        Assert.Equal(own.Location!.LocationId, Assert.Single(Assert.IsType<PropertyLocation[]>(result.Value)).LocationId);
    }

    [Fact]
    public async Task RepeatedLocationCaptureReusesIdentityAndAllowsMissingAdministrativeLevels()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var first = await PropertyLocationResolver.ResolveAsync(fixture.Properties, new PropertyLocation { CountryCode = "gb", Country = "United Kingdom", City = "London", Suburb = "Soho" });
        await fixture.Properties.SaveChangesAsync();
        var repeated = await PropertyLocationResolver.ResolveAsync(fixture.Properties, new PropertyLocation { CountryCode = "GB", Country = "United Kingdom", City = " london ", Suburb = "SOHO" });
        Assert.Same(first, repeated);
        Assert.NotEqual(first!.LocationId, PropertyLocationResolver.CreateIdentity(new PropertyLocation { CountryCode = "US", City = "London", Suburb = "Soho" }));
    }

    [Fact]
    public async Task NearbyLocationSelectionExcludesOtherAreasAndNamesakes()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var origin = fixture.AddProperty(); origin.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, Rosebank("Gauteng", "Johannesburg"));
        var nearby = fixture.AddProperty(latitude: -26.16m); nearby.Location = await PropertyLocationResolver.ResolveAsync(fixture.Properties, new PropertyLocation { CountryCode = "ZA", Country = "South Africa", Region = "Gauteng", City = "Johannesburg", Suburb = "Parktown" });
        fixture.AddProperty(latitude: -26.15m);
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { LocationId = origin.Location!.LocationId, SelectedNearbyLocationIds = [nearby.Location!.LocationId], IncludeNearby = true, RadiusKm = 10 }, default));
        var page = Assert.IsType<PublicSearchResult>(result.Value);
        Assert.Equal(2, page.Total);
        Assert.Contains(page.NearbyAreas!, area => area.LocationId == nearby.Location!.LocationId);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void SqlServerLocationQueriesSupportEmptyAndMixedSelections(bool exact, bool legacy)
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Property.Microservice.Data.PropertyDbContext>();
        Microsoft.EntityFrameworkCore.SqlServerDbContextOptionsExtensions.UseSqlServer(options, "Server=localhost;Database=LocationQueryCompileOnly;Integrated Security=true");
        using var database = new Property.Microservice.Data.PropertyDbContext(options.Options);
        var criteria = new Property.Microservice.DTOs.PropertySearchCriteria
        {
            LocationIds = exact ? ["test-location"] : [], Locations = legacy ? ["Lynnwood"] : []
        };
        var query = PropertyLocationSearchFilters.Apply(database.PropertyAssets, criteria);
        Assert.Contains("PropertyAssets", Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(query));
    }

    private static PropertyLocation Rosebank(string region, string city) => new() { CountryCode = "ZA", Country = "South Africa", Region = region, City = city, Suburb = "Rosebank" };
}
