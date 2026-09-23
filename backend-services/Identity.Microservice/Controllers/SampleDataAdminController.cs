using System.Security.Claims;
using System.Text.Json;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Access.SampleData;

namespace Identity.Microservice.Controllers;

[ApiController]
[Route("api/auth/sample-data")]
[Authorize(Roles = "PlatformAdmin,SuperAdmin")]
public sealed class SampleDataAdminController(IdentityDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var requests = await (from job in database.SampleDataJobs.AsNoTracking()
            join agency in database.TenantAgencies on job.TenantId equals agency.TenantId
            orderby job.CreatedAt descending
            select new { job.TenantId, agency.AgencyName, agency.HeadquartersAddress, job.State,
                job.CreatedAt, job.ProvisionedAt, job.NotifiedAt, job.Error, HasPlan = job.PlanJson != "" })
            .ToListAsync(cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{tenantId}/provision")]
    public async Task<IActionResult> Provision(string tenantId, ProvisionSampleDataRequest request, CancellationToken cancellationToken)
    {
        var job = await database.SampleDataJobs.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == tenantId, cancellationToken);
        if (job == null) return NotFound(new { message = "This agency has not requested sample data." });
        if (job.State is not ("Requested" or "Failed")) return Accepted(new { job.State });
        var agency = await database.TenantAgencies.SingleAsync(item => item.TenantId == tenantId, cancellationToken);
        var planJson = job.PlanJson;
        if (string.IsNullOrEmpty(planJson))
        {
            if (!request.Latitude.HasValue || !request.Longitude.HasValue)
                return BadRequest(new { message = "Confirm the agency address coordinates before provisioning." });
            var plan = new SampleDataPlan(tenantId, agency.AgencyName, agency.ContactEmail,
                agency.HeadquartersAddress, request.Latitude.Value, request.Longitude.Value, DateTime.UtcNow, agency.IsTrial, agency.TrialStartDate, agency.TrialEndDate);
            if (!SampleDataBlueprint.IsValid(plan)) return BadRequest(new { message = "A valid agency address and coordinates are required." });
            planJson = JsonSerializer.Serialize(plan);
        }
        var approver = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var changed = await database.SampleDataJobs.Where(item => item.TenantId == tenantId && (item.State == "Requested" || item.State == "Failed"))
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.State, "Pending")
                .SetProperty(item => item.PlanJson, planJson).SetProperty(item => item.ApprovedBy, approver)
                .SetProperty(item => item.AvailableAt, DateTime.UtcNow).SetProperty(item => item.Attempts, 0)
                .SetProperty(item => item.Error, (string?)null), cancellationToken);
        return Accepted(new { state = changed == 1 ? "Pending" : "Already queued" });
    }
}
