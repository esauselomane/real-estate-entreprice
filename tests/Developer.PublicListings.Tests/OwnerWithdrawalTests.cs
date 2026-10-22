using Developer.API.Controllers;
using Developer.API.DTOs;
using Developer.API.Models;
using Developer.API.Services.PublicListings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class OwnerWithdrawalTests
{
    [Fact]
    public async Task OwnersCanWithdrawOnlyTheirOwnListingsWithoutDeletingRecordsOrChangingHotStatus()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var owner = await fixture.AddOwnerAsync();
        var sessions = new OwnerSessionService(fixture.Developer);
        var token = await sessions.CreateAsync(owner, default);
        var property = fixture.AddProperty(hot: true); property.PublicOwnerAccountId = owner.Id;
        var unrelated = fixture.AddProperty();
        var crossAgency = fixture.AddProperty(agencyId: "AGENCY-B"); crossAgency.PublicOwnerAccountId = owner.Id;
        await fixture.Properties.SaveChangesAsync();
        var controller = new OwnerSubmissionsController(fixture.Properties, sessions);
        PublicListingFixture.SetAgency(controller);
        Assert.IsType<UnauthorizedResult>(await controller.Withdraw(property.PropertyId, default));
        PublicListingFixture.SetAgency(controller, ownerToken: token);
        Assert.IsType<NotFoundResult>(await controller.Withdraw(unrelated.PropertyId, default));
        Assert.IsType<NotFoundResult>(await controller.Withdraw(crossAgency.PropertyId, default));
        Assert.IsType<OkObjectResult>(await controller.Withdraw(property.PropertyId, default));
        Assert.False(property.IsPublicListing); Assert.Equal("Unpublished", property.PublicReviewStatus); Assert.True(property.IsHotProperty);
        Assert.Equal(3, await fixture.Properties.PropertyAssets.CountAsync());
        Assert.IsType<OkObjectResult>(await controller.Withdraw(property.PropertyId, default));
        var publicController = new DeveloperPropertiesController(fixture.Properties); PublicListingFixture.SetAgency(publicController);
        Assert.IsType<NotFoundResult>(await publicController.Detail(property.PropertyId, default));
        var hot = Assert.IsType<OkObjectResult>(await publicController.Hot(new PublicPropertySearch(), default));
        Assert.DoesNotContain(Assert.IsType<PublicSearchResult>(hot.Value).Items, item => item.Id == property.PropertyId);
        var list = Assert.IsType<OkObjectResult>(await controller.List(default));
        Assert.False(Assert.Single(Assert.IsType<List<PublicProperty>>(list.Value)).IsPublicListing);
        PublicListingFixture.SetAgency(controller, "AGENCY-B", token);
        Assert.IsType<UnauthorizedResult>(await controller.Withdraw(property.PropertyId, default));
    }
}
