using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Developer.API.Controllers;
using Developer.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shared.Access.PublicEnquiries;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class PublicEnquiryTests
{
    [Fact]
    public async Task PublicEnquiryEndpointRequiresAnAgencyApiKey()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Path = "/api/developer/enquiries";
        context.Response.Body = new MemoryStream();
        var middleware = new Developer.API.Middleware.ApiKeyAuthMiddleware(UnexpectedRequestAsync);
        await middleware.InvokeAsync(context, fixture.Developer);
        Assert.Equal(401, context.Response.StatusCode);
    }

    private static Task UnexpectedRequestAsync(Microsoft.AspNetCore.Http.HttpContext context)
    {
        throw new InvalidOperationException("An unauthenticated request must not reach the enquiry controller.");
    }

    [Fact]
    public void SignatureRejectsAlteredTenantPayloadTimestampAndMissingSecrets()
    {
        var enquiry = new WebsiteEnquiry { RequestId = Guid.NewGuid(), AgencyId = "AGENCY-A", PropertyId = 7, Name = "Visitor", Message = "Viewing", PropertyAddress = "Rosebank", PropertyType = "Room share/let" };
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = WebsiteEnquirySignature.Create(enquiry, timestamp, "test-only-secret");
        Assert.True(WebsiteEnquirySignature.Verify(enquiry, timestamp, signature, "test-only-secret"));
        Assert.False(WebsiteEnquirySignature.Verify(enquiry, timestamp, signature, null));
        Assert.False(WebsiteEnquirySignature.Verify(enquiry, timestamp, signature, "another-secret"));
        enquiry.AgencyId = "AGENCY-B";
        Assert.False(WebsiteEnquirySignature.Verify(enquiry, timestamp, signature, "test-only-secret"));
        enquiry.AgencyId = "AGENCY-A";
        var staleTime = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var staleSignature = WebsiteEnquirySignature.Create(enquiry, staleTime, "test-only-secret");
        Assert.False(WebsiteEnquirySignature.Verify(enquiry, staleTime, staleSignature, "test-only-secret"));
    }

    [Theory]
    [InlineData("Visitor", null, true)]
    [InlineData("", "+27821234567", true)]
    [InlineData("", null, false)]
    [InlineData(" ", " ", false)]
    public void EnquiryRequiresNameOrPhone(string name, string? phone, bool expected)
    {
        var request = new PublicEnquiryRequest { RequestId = Guid.NewGuid(), PropertyId = 7, Name = name, Phone = phone, Message = "Viewing" };
        var results = new List<ValidationResult>();
        Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), results, true));
    }

    [Fact]
    public async Task EnquiriesAreBoundToTheKeyAgencyAndApprovedInventory()
    {
        await using var fixture = new PublicListingFixture(); await fixture.InitializeAsync();
        var visible = fixture.AddProperty(); visible.IsForRent = true;
        var hidden = fixture.AddProperty(published: false);
        var foreign = fixture.AddProperty(agencyId: "AGENCY-B");
        var expired = fixture.AddProperty(); expired.IsForSale = false; expired.IsForRent = true; expired.PublicLettingExpiresAt = DateTime.UtcNow.AddDays(-1);
        await fixture.Properties.SaveChangesAsync();
        var handler = new EnquiryHttpHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CRM:BaseUrl"] = "https://crm.example", ["Jwt:Secret"] = "test-only-secret" }).Build();
        var controller = new PublicEnquiriesController(fixture.Properties, new EnquiryHttpClientFactory(handler), configuration);
        PublicListingFixture.SetAgency(controller);
        var request = new PublicEnquiryRequest { RequestId = Guid.NewGuid(), Name = "Visitor", Message = "Viewing", ListingType = "Rent" };
        foreach (var unavailable in new[] { hidden.PropertyId, foreign.PropertyId, expired.PropertyId })
        {
            request.PropertyId = unavailable;
            Assert.IsType<NotFoundObjectResult>(await controller.Create(request, default));
        }
        Assert.Equal(0, handler.Calls);
        request.PropertyId = visible.PropertyId;
        Assert.IsType<ContentResult>(await controller.Create(request, default));
        Assert.Equal(1, handler.Calls);
        Assert.Equal("AGENCY-A", handler.Enquiry!.AgencyId);
        Assert.Equal(visible.CompleteAddress, handler.Enquiry.PropertyAddress);
        Assert.Equal("ForRent", handler.Enquiry.LeadType);
        Assert.Equal(request.RequestId, handler.Enquiry.RequestId);
    }
}
