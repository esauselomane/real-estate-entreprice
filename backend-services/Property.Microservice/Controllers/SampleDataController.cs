using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Property.Microservice.Services.SampleData;
using Shared.Access.SampleData;

namespace Property.Microservice.Controllers;

[ApiController, Route("api/internal/sample-data"), SampleDataEndpoint]
[Authorize(Roles = SampleDataBlueprint.ServiceRole)]
public sealed class SampleDataController(SampleDataSeeder seeder) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(SampleDataPlan plan, CancellationToken cancellationToken)
    {
        if (User.FindFirst("TenantId")?.Value != plan.TenantId) return Forbid();
        if (!SampleDataBlueprint.IsValid(plan)) return BadRequest();
        return Ok(await seeder.SeedAsync(plan, cancellationToken));
    }
}
