using System.Globalization;
using CRM.Microservice.Controllers;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shared.Access.PublicEnquiries;
using Xunit;

namespace Developer.PublicListings.Tests;

public sealed class CrmEnquiryDeliveryTests
{
    [Fact]
    public async Task ConfirmedEnquiryCreatesOneAgencyLeadWithoutCreatingContactsAndRejectsTampering()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var database = new CRMDbContext(new DbContextOptionsBuilder<CRMDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = "test-only-secret" }).Build();
        var controller = new WebsiteEnquiriesController(database, configuration);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var enquiry = new WebsiteEnquiry { RequestId = Guid.NewGuid(), AgencyId = "AGENCY-A", PropertyId = 7, Name = "Visitor", Surname = "Example", Phone = "+27821234567", Message = "Please arrange a viewing", PropertyAddress = "Rosebank, Gauteng", PropertyType = "Room share/let", LeadType = "ForRent", ReferralCode = "MARKETER_1" };
        Assert.IsType<UnauthorizedResult>(await controller.Create(enquiry, default));
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        controller.Request.Headers["X-Enquiry-Time"] = timestamp;
        controller.Request.Headers["X-Enquiry-Signature"] = WebsiteEnquirySignature.Create(enquiry, timestamp, "test-only-secret");
        Assert.IsType<OkObjectResult>(await controller.Create(enquiry, default));
        Assert.IsType<OkObjectResult>(await controller.Create(enquiry, default));
        var lead = Assert.Single(await database.LeadRecords.ToArrayAsync());
        Assert.Equal("AGENCY-A", lead.TenantId);
        Assert.Equal("Visitor Example", lead.ClientName);
        Assert.Equal("ForRent", lead.LeadType);
        Assert.Equal("Room share/let", lead.PropertyInterestClass);
        Assert.Contains("MARKETER_1", lead.LeadNotes);
        Assert.Null(lead.LinkedContactId);
        Assert.Empty(await database.CRMContacts.ToArrayAsync());
        Assert.False(lead.IsConvertedToDeal);
        enquiry.AgencyId = "AGENCY-B";
        Assert.IsType<UnauthorizedResult>(await controller.Create(enquiry, default));
        Assert.Single(await database.LeadRecords.ToArrayAsync());
    }
}
