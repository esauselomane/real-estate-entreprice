using System.Net.Http.Json;
using System.Globalization;
using Developer.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Shared.Access.PublicEnquiries;

namespace Developer.API.Controllers;

[ApiController]
[Route("api/developer/enquiries")]
[EnableRateLimiting("owner-auth")]
public sealed class PublicEnquiriesController(PropertyDbContext database, IHttpClientFactory clients, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(PublicEnquiryRequest request, CancellationToken cancellationToken)
    {
        var agencyId = (string)HttpContext.Items["AgencyId"]!;
        var property = await database.PropertyAssets.AsNoTracking().Include(item => item.PropertyTypeDetails)
            .Where(item => item.PropertyId == request.PropertyId && item.TenantId == agencyId)
            .Where(item => item.IsPublicListing && item.PublicReviewStatus == "Approved")
            .FirstOrDefaultAsync(cancellationToken);
        if (property is not null && request.ListingType == "Sale" && !property.IsForSale) property = null;
        if (property is not null && request.ListingType == "Rent" && (!property.IsForRent || property.PublicLettingExpiresAt <= DateTime.UtcNow)) property = null;
        if (property is null) return NotFound(new { message = "This property is no longer available." });
        var secret = configuration["Jwt:Secret"];
        var crmUrl = configuration["CRM:BaseUrl"];
        if (string.IsNullOrWhiteSpace(secret) || !Uri.TryCreate(crmUrl, UriKind.Absolute, out var crmUri))
            return StatusCode(503, new { message = "Enquiry delivery is not configured." });
        var enquiry = new WebsiteEnquiry
        {
            RequestId = request.RequestId, AgencyId = agencyId, PropertyId = property.PropertyId,
            Name = request.Name, Surname = request.Surname, Email = request.Email, Phone = request.Phone,
            Message = request.Message, ReferralCode = request.ReferralCode,
            PropertyAddress = property.CompleteAddress, PropertyType = property.PropertyTypeDetails?.Name ?? "Property",
            LeadType = request.ListingType == "Sale" ? "ForSale" : "ForRent"
        };
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var upstreamRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(crmUri, "/api/crm/website-enquiries"));
        upstreamRequest.Content = JsonContent.Create(enquiry);
        upstreamRequest.Headers.Add("X-Enquiry-Time", timestamp);
        upstreamRequest.Headers.Add("X-Enquiry-Signature", WebsiteEnquirySignature.Create(enquiry, timestamp, secret));
        using var client = clients.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        using var response = await client.SendAsync(upstreamRequest, cancellationToken);
        if (!response.IsSuccessStatusCode) return StatusCode(502, new { message = "The agency could not receive your enquiry. Please try again." });
        return Content(await response.Content.ReadAsStringAsync(cancellationToken), "application/json");
    }
}
