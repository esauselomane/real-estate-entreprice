using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs.Syndication;
using Property.Microservice.Models.Syndication;
using Property.Microservice.Services;
using Property.Microservice.Services.Abstractions;
using Property.Microservice.Services.Syndication;

namespace Property.Microservice.Controllers;

[Authorize]
[Route("api/properties/syndication")]
public sealed class SyndicationController(PropertyDbContext database, PermissionCheckService permissions,
    IPropertyRepository properties, IPropertyUnitRepository units, ILogger<SyndicationController> logger,
    ListingProviderRegistry registry, SyndicationCoordinator coordinator)
    : PropertyControllerBase(database, permissions, properties, units, logger)
{
    [HttpGet("{propertyId:int}")]
    public async Task<IActionResult> GetStatus(int propertyId, CancellationToken cancellationToken)
    {
        var property = await ApplyPropertyVisibility(DbContext.PropertyAssets).FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);
        if (property == null) return NotFound();
        var connections = await DbContext.SyndicationConnections.AsNoTracking().Where(item => item.TenantId == property.TenantId).ToListAsync(cancellationToken);
        var publications = await DbContext.SyndicationPublications.AsNoTracking().Where(item => item.PropertyId == propertyId && item.TenantId == property.TenantId).ToListAsync(cancellationToken);
        return Ok(new { providers = registry.All.Select(item => item.Descriptor), connections, publications, listing = SyndicationCoordinator.CreateSnapshot(property) });
    }

    [HttpGet("providers")]
    public IActionResult GetProviders() => Ok(registry.All.Select(item => item.Descriptor));

    [HttpGet("agencies/{tenantId}")]
    public async Task<IActionResult> GetAgencyConnections(string tenantId, CancellationToken cancellationToken)
    {
        if (!CanManageAgency(tenantId)) return Forbid();
        var connections = await DbContext.SyndicationConnections.AsNoTracking()
            .Where(item => item.TenantId == tenantId).ToListAsync(cancellationToken);
        return Ok(new { providers = registry.All.Select(item => item.Descriptor), connections });
    }

    [HttpPut("agencies/{tenantId}/{providerKey}")]
    public async Task<IActionResult> SaveAgencyConnection(string tenantId, string providerKey, AgencyConnectionRequest request,
        [FromServices] AgencyApiKeyProtector protector, CancellationToken cancellationToken)
    {
        if (!CanManageAgency(tenantId) || !await PermissionCheck.HasPermissionAsync("agencies:edit")) return Forbid();
        if (request.Environment is not ("Sandbox" or "Production")) return BadRequest(new { message = "Choose Sandbox or Production." });
        if (!registry.All.Any(item => item.Descriptor.Key == providerKey)) return NotFound();
        if ((request.ApiKey?.Length ?? 0) > 4096 || (request.BranchId?.Length ?? 0) > 200)
            return BadRequest(new { message = "API key or branch ID is too long." });
        if (request.RemoveApiKey && !string.IsNullOrWhiteSpace(request.ApiKey))
            return BadRequest(new { message = "Choose either replacing or removing the API key." });
        await using var transaction = await DbContext.Database.BeginTransactionAsync(cancellationToken);
        await SyndicationCoordinator.AcquireLockAsync(DbContext, cancellationToken);
        var connection = await DbContext.SyndicationConnections.SingleOrDefaultAsync(item => item.TenantId == tenantId &&
            item.ProviderKey == providerKey && item.Environment == request.Environment, cancellationToken);
        if (connection == null)
        {
            connection = new SyndicationConnection { TenantId = tenantId, ProviderKey = providerKey, Environment = request.Environment };
            DbContext.SyndicationConnections.Add(connection);
        }
        var descriptor = registry.Get(providerKey).Descriptor;
        var settings = request.Settings ?? JsonSerializer.Deserialize<Dictionary<string, string>>(connection.SettingsJson) ?? [];
        var allowedSettings = descriptor.Settings ?? [];
        if (settings.Count > 30 || settings.Any(item => item.Value == null || item.Value.Length > 500 || !allowedSettings.Any(field => field.Key == item.Key)))
            return BadRequest(new { message = "Connection settings contain unsupported fields or overly long values." });
        var serializedSettings = JsonSerializer.Serialize(settings.OrderBy(item => item.Key).ToDictionary(item => item.Key, item => item.Value));
        if (serializedSettings != connection.SettingsJson && await DbContext.SyndicationPublications.AnyAsync(item => item.ConnectionId == connection.Id, cancellationToken))
            return Conflict(new { message = "Reconcile existing publications before changing portal mappings." });
        var branchId = request.BranchId?.Trim() ?? string.Empty;
        if (connection.BranchId != branchId && await DbContext.SyndicationPublications.AnyAsync(item => item.ConnectionId == connection.Id, cancellationToken))
            return Conflict(new { message = "Reconcile existing publications before changing the branch mapping." });
        try
        {
            if (request.RemoveApiKey) connection.ProtectedApiKey = string.Empty;
            else if (!string.IsNullOrWhiteSpace(request.ApiKey)) connection.ProtectedApiKey = protector.Protect(connection, request.ApiKey.Trim());
        }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        connection.BranchId = branchId;
        connection.SettingsJson = serializedSettings;
        connection.Enabled = connection.HasApiKey;
        await DbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(connection);
    }

    private bool CanManageAgency(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId == "__all__" || tenantId.Length > 100) return false;
        var role = GetCurrentCanonicalRole();
        if (role == "PlatformAdmin") return true;
        if (role != "AgencyAdmin") return false;
        var visibleTenantIds = HttpContext.Items["VisibleTenantIds"] as List<string>
            ?? [User.FindFirst("TenantId")?.Value ?? string.Empty];
        return visibleTenantIds.Contains(tenantId, StringComparer.OrdinalIgnoreCase);
    }

    [HttpPost("{propertyId:int}/connections/{connectionId:int}/test")]
    public async Task<IActionResult> TestConnection(int propertyId, int connectionId, CancellationToken cancellationToken)
    {
        if (GetCurrentCanonicalRole() is not ("PlatformAdmin" or "AgencyAdmin")) return Forbid();
        if (!await PermissionCheck.HasPermissionAsync("properties:edit")) return Forbid();
        var property = await ApplyPropertyVisibility(DbContext.PropertyAssets).FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);
        if (property == null) return NotFound();
        var connection = await DbContext.SyndicationConnections.SingleOrDefaultAsync(item => item.Id == connectionId && item.TenantId == property.TenantId, cancellationToken);
        if (connection == null) return NotFound();
        var issues = await registry.Get(connection.ProviderKey).TestConnectionAsync(connection, cancellationToken);
        return Ok(new { success = issues.Count == 0, issues });
    }

    [HttpPost("{propertyId:int}/connections/{connectionId:int}/publish")]
    public async Task<IActionResult> Publish(int propertyId, int connectionId, PublicationRequest request, CancellationToken cancellationToken)
    {
        if (IsReadOnlyRole() || !await PermissionCheck.HasPermissionAsync("properties:edit")) return Forbid();
        var property = await ApplyPropertyVisibility(DbContext.PropertyAssets).FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);
        if (property == null) return NotFound();
        var connection = await DbContext.SyndicationConnections.SingleOrDefaultAsync(item => item.Id == connectionId && item.TenantId == property.TenantId, cancellationToken);
        if (connection == null) return NotFound();
        try
        {
            var publication = await coordinator.QueueAsync(property, connection, request.Publish, cancellationToken);
            return Accepted(publication);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
    [HttpPost("{propertyId:int}/connections/{connectionId:int}/reconcile")]
    public async Task<IActionResult> Reconcile(int propertyId, int connectionId, CancellationToken cancellationToken)
    {
        if (IsReadOnlyRole() || !await PermissionCheck.HasPermissionAsync("properties:edit")) return Forbid();
        var property = await ApplyPropertyVisibility(DbContext.PropertyAssets).FirstOrDefaultAsync(item => item.PropertyId == propertyId, cancellationToken);
        if (property == null) return NotFound();
        var connection = await DbContext.SyndicationConnections.SingleOrDefaultAsync(item => item.Id == connectionId && item.TenantId == property.TenantId, cancellationToken);
        if (connection == null) return NotFound();
        try
        {
            var publication = await coordinator.QueueAsync(property, connection, false, cancellationToken, reconcile: true);
            return Accepted(publication);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
