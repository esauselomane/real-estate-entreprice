using System.Text.Json;
using Identity.Microservice.Data;
using Identity.Microservice.DTOs;
using Identity.Microservice.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Identity.Microservice.Controllers;

/// <summary>SuperAdmin-only catalogue maintenance; package codes and existing payment agreements remain stable.</summary>
[ApiController]
[Route("api/system/pricing-packages")]
[Authorize(Roles = "SuperAdmin")]
public sealed class PricingPackagesController(IdentityDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var packages = await database.PricingPackages.AsNoTracking().OrderBy(package => package.SortOrder).ToListAsync();
        return Ok(packages.Select(ToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Create(PricingPackageAdminRequest request)
    {
        if (await database.PricingPackages.AnyAsync(package => package.Code == request.Code))
            return Conflict(new { message = "Package code already exists." });
        var package = new PricingPackage { Code = request.Code };
        Apply(package, request);
        database.PricingPackages.Add(package);
        await database.SaveChangesAsync();
        return Ok(ToResponse(package));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PricingPackageAdminRequest request)
    {
        var package = await database.PricingPackages.FindAsync(id);
        if (package == null) return NotFound();
        if (package.Code != request.Code)
            return BadRequest(new { message = "Package codes cannot change. Deactivate the old package and create a new one." });
        Apply(package, request);
        package.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync();
        return Ok(ToResponse(package));
    }

    private static void Apply(PricingPackage package, PricingPackageAdminRequest request)
    {
        package.Name = request.Name.Trim();
        package.Description = request.Description?.Trim();
        package.MonthlyPrice = request.MonthlyPrice;
        package.AnnualPrice = request.AnnualPrice;
        package.FeaturesJson = JsonSerializer.Serialize(request.Features.Where(feature => !string.IsNullOrWhiteSpace(feature)).Select(feature => feature.Trim()).Distinct());
        package.SortOrder = request.SortOrder;
        package.IsActive = request.IsActive;
    }

    private static object ToResponse(PricingPackage package) => new
    {
        package.Id, package.Code, package.Name, package.Description, package.MonthlyPrice,
        package.AnnualPrice, package.SortOrder, package.IsActive,
        features = JsonSerializer.Deserialize<string[]>(package.FeaturesJson ?? "[]") ?? []
    };
}
