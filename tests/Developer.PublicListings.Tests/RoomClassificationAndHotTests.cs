using Developer.API.Controllers;
using Developer.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Services.PublicListings;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class RoomClassificationAndHotTests
{
    [Theory]
    [InlineData("AgencyAdmin", "A", "A", true)]
    [InlineData("PlatformAdmin", "A", "A", true)]
    [InlineData("AgencyAdmin", "A", "B", false)]
    [InlineData("PlatformAdmin", "A", "B", false)]
    [InlineData("Broker", "A", "A", false)]
    [InlineData("Owner", "A", "A", false)]
    [InlineData("AgencyAdmin", null, "A", false)]
    public void HotStatusRequiresAnAdministratorBelongingToThePropertyAgency(string role, string? callerAgency, string propertyAgency, bool expected)
    {
        Assert.Equal(expected, HotPropertyAuthorization.CanManage(role, callerAgency, propertyAgency));
    }

    [Fact]
    public async Task RoomCatalogueMigrationPreservesPurposeVisibilityAndHotStatus()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var room = fixture.AddProperty(hot: true); room.RoomLettingType = "PrivateRoom";
        var house = fixture.AddProperty();
        await fixture.Properties.SaveChangesAsync();
        PublicListingSchema.EnsurePropertyTypes(fixture.Properties);
        fixture.Properties.ChangeTracker.Clear();
        var migrated = await fixture.Properties.PropertyAssets.SingleAsync(item => item.PropertyId == room.PropertyId);
        Assert.Equal("Room share/let", migrated.PropertyTypeDetails?.Name ?? (await fixture.Properties.PropertyTypes.FindAsync(migrated.PropertyTypeId))!.Name);
        Assert.True(migrated.IsForSale); Assert.False(migrated.IsForRent); Assert.True(migrated.IsPublicListing); Assert.True(migrated.IsHotProperty);
        Assert.Equal(1, (await fixture.Properties.PropertyAssets.FindAsync(house.PropertyId))!.PropertyTypeId);
    }

    [Fact]
    public async Task RoomTypeCanBeSearchedForSaleOrForLetWithoutMixingHouseListings()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var room = fixture.AddProperty(); room.PropertyTypeId = 2; room.RoomLettingType = "PrivateRoom";
        fixture.AddProperty();
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { PropertyType = "Room share/let", ListingType = "Sale" }, default));
        Assert.Equal(room.PropertyId, Assert.Single(Assert.IsType<PublicSearchResult>(result.Value).Items).Id);
        result = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { PropertyType = "Room share/let", ListingType = "Rent" }, default));
        Assert.Empty(Assert.IsType<PublicSearchResult>(result.Value).Items);
    }
}
