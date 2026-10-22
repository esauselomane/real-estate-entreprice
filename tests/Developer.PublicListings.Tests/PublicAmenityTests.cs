using System.Text.Json;
using Developer.API.Controllers;
using Developer.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Property.Microservice.Models;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class PublicAmenityTests
{
    [Fact]
    public async Task DetailsPublishActiveAmenitiesAtTheirOwnScopeAndOnlyAdvertisedDeposits()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var property = fixture.AddProperty();
        property.SubUnits.Add(new PropertyUnit { UnitNumber = "1", UnitDescription = "Test unit", AdvertisedDepositAmount = 12500m });
        var parking = new AmenityType { Name = "Parking", Label = "Parking", RequiresNumber = true };
        var pets = new AmenityType { Name = "PetsAllowed", Label = "Pets allowed", RequiresBoolean = true };
        var inactive = new AmenityType { Name = "PrivateFacility", IsActive = false };
        fixture.Properties.AmenityTypes.AddRange(parking, pets, inactive);
        await fixture.Properties.SaveChangesAsync();
        fixture.Properties.PropertyUnitAmenities.AddRange(
            new PropertyUnitAmenity { PropertyId = property.PropertyId, TenantId = "AGENCY-A", AmenityTypeId = parking.AmenityTypeId, NumberValue = 2, Notes = "internal-notes" },
            new PropertyUnitAmenity { PropertyId = property.PropertyId, PropertyUnitId = property.SubUnits.First().PropertyUnitId, TenantId = "AGENCY-A", AmenityTypeId = pets.AmenityTypeId, BooleanValue = false },
            new PropertyUnitAmenity { PropertyId = property.PropertyId, TenantId = "AGENCY-A", AmenityTypeId = inactive.AmenityTypeId },
            new PropertyUnitAmenity { PropertyId = property.PropertyId, TenantId = "AGENCY-B", AmenityTypeId = pets.AmenityTypeId, BooleanValue = true });
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        var result = Assert.IsType<OkObjectResult>(await controller.Detail(property.PropertyId, default));
        var detail = Assert.IsType<PublicProperty>(result.Value);
        Assert.Equal(2, Assert.Single(detail.Amenities!).NumberValue);
        Assert.Equal(12500m, Assert.Single(detail.Units).AdvertisedDepositAmount);
        Assert.False(Assert.Single(detail.Units[0].Amenities!).BooleanValue);
        Assert.DoesNotContain("internal-notes", JsonSerializer.Serialize(detail));
        Assert.DoesNotContain("PrivateFacility", JsonSerializer.Serialize(detail));
    }
}
