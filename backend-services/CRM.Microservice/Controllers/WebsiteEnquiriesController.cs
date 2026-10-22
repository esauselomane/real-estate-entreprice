using System.Data;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Access.PublicEnquiries;

namespace CRM.Microservice.Controllers;

/// <summary>Receives signed, agency-scoped enquiries from the Developer API without creating CRM contacts.</summary>
[ApiController]
[Route("api/crm/website-enquiries")]
public sealed class WebsiteEnquiriesController(CRMDbContext database, IConfiguration configuration) : ControllerBase
{
    // This endpoint uses a purpose-specific payload signature, not an end-user bearer token.
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create(WebsiteEnquiry enquiry, CancellationToken cancellationToken)
    {
        if (!WebsiteEnquirySignature.Verify(enquiry, Request.Headers["X-Enquiry-Time"].ToString(),
                Request.Headers["X-Enquiry-Signature"].ToString(), configuration["Jwt:Secret"]))
            return Unauthorized();

        var marker = $"[Website enquiry {enquiry.RequestId:D}]";
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var previous = await database.LeadRecords.FirstOrDefaultAsync(item => item.TenantId == enquiry.AgencyId && item.LeadNotes.StartsWith(marker), cancellationToken);
        if (previous is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { leadId = previous.LeadId });
        }

        var lead = new LeadRecord
        {
            TenantId = enquiry.AgencyId,
            ClientName = string.Join(" ", new[] { enquiry.Name.Trim(), enquiry.Surname.Trim() }.Where(part => part.Length > 0)),
            ContactEmail = enquiry.Email?.Trim() ?? "",
            ContactPhone = enquiry.Phone?.Trim() ?? "",
            PropertyInterestClass = enquiry.PropertyType,
            PreferredArea = enquiry.PropertyAddress,
            LeadType = enquiry.LeadType,
            LeadStage = LeadWorkflowStage.Contacted,
            LeadNotes = $"{marker}\nProperty #{enquiry.PropertyId}: {enquiry.PropertyAddress}\nReferral: {enquiry.ReferralCode}\n{enquiry.Message}",
            CreatedAt = DateTime.UtcNow
        };
        database.LeadRecords.Add(lead);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { leadId = lead.LeadId });
    }
}
