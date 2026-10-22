using Developer.API.Controllers;
using Developer.API.DTOs;
using Developer.API.Services.PublicListings;
using Microsoft.AspNetCore.Mvc;
using Property.Microservice.Models;
using Property.Microservice.Services.Images;
using Property.Microservice.Services.PublicListings;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class ListingTermAndImageTests
{
    [Fact]
    public void PublicationUsesCalendarMonthsAndOrdinaryEditsDoNotRenew()
    {
        var property = new PropertyAsset { IsForRent = true };
        var start = new DateTime(2026, 12, 31, 10, 0, 0, DateTimeKind.Utc);
        PublicListingTerm.Publish(property, start, false);
        Assert.Equal(new DateTime(2027, 2, 28, 10, 0, 0, DateTimeKind.Utc), property.PublicLettingExpiresAt);
        PublicListingTerm.Publish(property, start.AddDays(10), false);
        Assert.Equal(start.AddMonths(2), property.PublicLettingExpiresAt);
        PublicListingTerm.Publish(property, start.AddDays(10), true);
        Assert.Equal(start.AddDays(10).AddMonths(2), property.PublicLettingExpiresAt);
    }

    [Fact]
    public async Task ExpiredRentalIsHiddenImmediatelyButMixedListingRemainsForSale()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var rental = fixture.AddProperty(); rental.IsForSale = false; rental.IsForRent = true; rental.PublicLettingExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var mixed = fixture.AddProperty(); mixed.IsForRent = true; mixed.PublicLettingExpiresAt = rental.PublicLettingExpiresAt;
        await fixture.Properties.SaveChangesAsync();
        var controller = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(controller);
        Assert.IsType<NotFoundResult>(await controller.Detail(rental.PropertyId, default));
        var detail = Assert.IsType<OkObjectResult>(await controller.Detail(mixed.PropertyId, default));
        Assert.True(Assert.IsType<PublicProperty>(detail.Value).IsForSale); Assert.False(Assert.IsType<PublicProperty>(detail.Value).IsForRent);
        var search = Assert.IsType<OkObjectResult>(await controller.Search(new PublicPropertySearch { ListingType = "Rent" }, default));
        Assert.Empty(Assert.IsType<PublicSearchResult>(search.Value).Items);
    }

    [Fact]
    public void SharedImageProcessingNormalisesValidPhotosAndRejectsOversizeOrInvalidContent()
    {
        using var image = new Image<Rgb24>(1800, 800);
        using var stream = new MemoryStream(); image.SaveAsPng(stream);
        var processed = PropertyImageProcessor.Process("data:image/png;base64," + Convert.ToBase64String(stream.ToArray()));
        Assert.StartsWith("data:image/jpeg;base64,", processed);
        var decoded = Image.Identify(Convert.FromBase64String(processed[(processed.IndexOf(',') + 1)..]));
        Assert.Equal(1200, decoded.Width);
        Assert.Throws<ArgumentException>(() => PropertyImageProcessor.Process("data:image/png;base64," + Convert.ToBase64String(new byte[2 * 1024 * 1024 + 1])));
        Assert.Throws<ArgumentException>(() => PropertyImageProcessor.Process("data:image/png;base64,bm90IGFuIGltYWdl"));
    }

    [Fact]
    public async Task OwnerUploadsHaveOneTenPhotoLimitAcrossPropertyAndUnits()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var owner = await fixture.AddOwnerAsync(); var sessions = new OwnerSessionService(fixture.Developer);
        var token = await sessions.CreateAsync(owner, default);
        var controller = new OwnerSubmissionsController(fixture.Properties, sessions); PublicListingFixture.SetAgency(controller, ownerToken: token);
        var request = new OwnerPropertySubmission { Address = "An owner listing", Description = "A home", PropertyType = "Residential", ErfSize = "100 m²", IsForSale = true, SalePrice = 500000, PublishContactConsent = true };
        request.Images = Enumerable.Repeat("data:image/png;base64,invalid", 10).ToList();
        request.Units.Add(new OwnerUnitSubmission { Number = "1", Description = "Home", Images = ["data:image/png;base64,invalid"] });
        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(request, default));
        Assert.Contains("10 photographs", result.Value!.ToString());
    }
}
