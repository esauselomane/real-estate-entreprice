using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Property.Microservice.Data;
using Property.Microservice.DTOs;
using Property.Microservice.Models;
using Property.Microservice.Services.Abstractions;
using Property.Microservice.Services;
using System.Security.Claims;
using System.Text.Json;

namespace Property.Microservice.Controllers;

/// <summary>
/// Shared base controller for all property-service controllers.
/// Centralises tenant/role resolution, visibility scoping, and document
/// JSON helpers so derived controllers stay thin and focused.
/// </summary>
[ApiController]
public abstract class PropertyControllerBase : ControllerBase
{
    protected readonly PropertyDbContext DbContext;
    protected readonly KafkaPublisherService KafkaPublisher;
    protected readonly IPropertyRepository PropertyRepository;
    protected readonly IPropertyUnitRepository PropertyUnitRepository;
    protected readonly PermissionCheckService PermissionCheck;
    protected readonly ILogger Logger;
    protected readonly string StorageRootPath;

    protected static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    protected PropertyControllerBase(
        PropertyDbContext dbContext,
        KafkaPublisherService kafkaPublisher,
        PermissionCheckService permissionCheck,
        IPropertyRepository propertyRepository,
        IPropertyUnitRepository propertyUnitRepository,
        ILogger logger)
    {
        DbContext = dbContext;
        KafkaPublisher = kafkaPublisher;
        PermissionCheck = permissionCheck;
        PropertyRepository = propertyRepository;
        PropertyUnitRepository = propertyUnitRepository;
        Logger = logger;

        StorageRootPath = Path.Combine(AppContext.BaseDirectory, "PropertyMediaVault");
        if (!Directory.Exists(StorageRootPath))
        {
            Directory.CreateDirectory(StorageRootPath);
        }
    }

    /// <summary>
    /// Resolve the caller's canonical role, normalizing legacy seed aliases
    /// (SuperAdmin/Manager/Agent/Admin) to the advertised 5-role contract.
    /// </summary>
    protected string? GetCurrentCanonicalRole()
    {
        var role = User.FindFirst(c =>
            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
            ?? User.FindFirst(ClaimTypes.Role)?.Value;
        return role switch
        {
            "SuperAdmin" => "PlatformAdmin",
            "Manager" => "AgencyAdmin",
            "Admin" => "AgencyAdmin",
            "Agent" => "Broker",
            _ => role
        };
    }

    private static readonly System.Collections.Generic.HashSet<string> ReadOnlyRoles =
        new(System.StringComparer.OrdinalIgnoreCase) { "Tenant", "Owner" };

    /// <summary>
    /// Tenants and Owners may never delete anything (units, documents, maintenance calls).
    /// </summary>
    protected bool IsReadOnlyRole() => ReadOnlyRoles.Contains(GetCurrentCanonicalRole() ?? string.Empty);

    /// <summary>
    /// Apply role + tenant scoping to a PropertyAsset queryable so search / look-ups
    /// only surface properties a role is entitled to see. When the caller is a
    /// PlatformAdmin/AgencyAdmin and supplies ?tenantId=<id>, that override is
    /// honored in place of the JWT-bound tenantId (the JWT tenant itself is
    /// never mutated). The override value is a real TenantId drawn from the
    /// AgencyRecord.TenantId of the selected agency.
    /// </summary>
    /// <remarks>
    /// Subtree support: the override may also be a comma-separated list
    /// of TenantIds (e.g. ?tenantId=TEN-0001,TEN-0010,TEN-0100). This is
    /// how the CRM hands a parent-agency's full subtree down to the
    /// property service for dashboard aggregation — the property service
    /// doesn't know about agency hierarchy on its own, so the CRM expands
    /// the subtree and the property service filters on the resulting set.
    /// </remarks>
    protected IQueryable<PropertyAsset> ApplyPropertyVisibility(IQueryable<PropertyAsset> query)
    {
        var role = GetCurrentCanonicalRole();
        var overrideTenantId = HttpContext?.Request?.Query["tenantId"].ToString();
        var usingOverride = (role == "PlatformAdmin" || role == "AgencyAdmin")
            && !string.IsNullOrWhiteSpace(overrideTenantId)
            && overrideTenantId != "__all__";

        if (role == "PlatformAdmin" && !usingOverride) return query;

        if (usingOverride)
        {
            var tenantIds = overrideTenantId
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tenantIds.Count == 0) return query.Where(p => false);
            if (tenantIds.Count == 1) query = query.Where(p => p.TenantId == tenantIds[0]);
            else query = query.Where(p => tenantIds.Contains(p.TenantId));
            return query;
        }

        var tenantId = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrWhiteSpace(tenantId)) return query.Where(p => false);
        query = query.Where(p => p.TenantId == tenantId);

        if (role == "Owner")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(p => p.LinkedOwnerId == userId);
        }
        else if (role == "Tenant")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var leasedPropertyIds = DbContext.UnitLeases
                .Where(l => l.UserId == userId && l.TenantId == tenantId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow)
                .Select(l => l.PropertyId).ToList();
            query = query.Where(p => leasedPropertyIds.Contains(p.PropertyId));
        }
        return query;
    }

    /// <summary>
    /// Resolves a unit that the caller is authorized to access, applying
    /// tenant + role scoping.
    /// </summary>
    /// <remarks>
    /// Access matrix:
    ///   • PlatformAdmin / AgencyAdmin -> any unit within the caller's tenant
    ///   • Broker                        -> any unit within the caller's tenant
    ///   • Owner                         -> only units belonging to a property they own (LinkedOwnerId)
    ///   • Tenant                        -> only the unit they currently have an ACTIVE lease on
    /// </remarks>
    protected async Task<PropertyUnit?> GetAccessibleUnitAsync(int propertyId, int unitId)
    {
        var role = GetCurrentCanonicalRole();
        var userTenantId = User.FindFirst("TenantId")?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userTenantId) && role != "PlatformAdmin") return null;

        var overrideTenantId = HttpContext?.Request?.Query["tenantId"].ToString();
        var usingOverride = (role == "PlatformAdmin" || role == "AgencyAdmin")
            && !string.IsNullOrWhiteSpace(overrideTenantId)
            && overrideTenantId != "__all__";
        var effectiveTenantId = usingOverride ? overrideTenantId : userTenantId;

        var isPlatformAdmin = role == "PlatformAdmin";
        var propertyQuery = PropertyRepository.Query().Where(p => p.PropertyId == propertyId);
        if (usingOverride)
        {
            var tenantIds = overrideTenantId
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (tenantIds.Count == 1) propertyQuery = propertyQuery.Where(p => p.TenantId == tenantIds[0]);
            else if (tenantIds.Count > 1) propertyQuery = propertyQuery.Where(p => tenantIds.Contains(p.TenantId));
        }
        else if (!isPlatformAdmin)
        {
            propertyQuery = propertyQuery.Where(p => p.TenantId == userTenantId);
        }

        if (role == "Owner" && !string.IsNullOrWhiteSpace(userId))
            propertyQuery = propertyQuery.Where(p => p.LinkedOwnerId == userId);

        var propertyExists = await propertyQuery.AnyAsync();
        if (!propertyExists) return null;

        var unit = await PropertyUnitRepository.Query().FirstOrDefaultAsync(u => u.PropertyUnitId == unitId && u.PropertyId == propertyId);
        if (unit == null) return null;

        if (role == "Tenant")
        {
            var isLeased = await DbContext.UnitLeases.AnyAsync(l =>
                l.UserId == userId && l.TenantId == userTenantId && l.PropertyUnitId == unitId &&
                l.PropertyId == propertyId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow);
            if (!isLeased) return null;
        }

        return unit;
    }

    protected async Task<HashSet<int>> GetLeasedUnitIdsAsync(string? userId, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tenantId))
            return new HashSet<int>();

        return await DbContext.UnitLeases
            .Where(l => l.UserId == userId && l.TenantId == tenantId && l.IsActive && l.LeaseEnd >= DateTime.UtcNow)
            .Select(l => l.PropertyUnitId)
            .ToHashSetAsync();
    }

    protected static List<DocumentMeta> ParseUnitDocuments(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]") return new List<DocumentMeta>();
        try { return JsonSerializer.Deserialize<List<DocumentMeta>>(json, JsonOptions) ?? new List<DocumentMeta>(); }
        catch { return new List<DocumentMeta>(); }
    }
}
