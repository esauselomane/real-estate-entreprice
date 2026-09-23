using CRM.Microservice.Services.SampleData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Access.SampleData;

namespace CRM.Microservice.Controllers;

[ApiController, Route("api/internal/sample-data"), SampleDataEndpoint]
[Authorize(Roles = SampleDataBlueprint.ServiceRole)]
public sealed class SampleDataController(SampleDataSeeder seeder) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CrmSampleDataRequest request, CancellationToken cancellationToken)
    {
        if (User.FindFirst("TenantId")?.Value != request.Plan.TenantId) return Forbid();
        try { SampleDataSeeder.Validate(request); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        await seeder.SeedAsync(request, cancellationToken);
        return Ok(new { status = "Completed" });
    }
}
