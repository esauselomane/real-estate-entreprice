using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;
using CRM.Microservice.Services;

namespace CRM.Microservice.Controllers;

/// <summary>
/// Shared base controller for all CRM-service controllers.
/// Centralises tenant/role resolution, tenant-subtree visibility,
/// pagination, and aggregate helpers so derived controllers stay thin.
/// </summary>
[ApiController]
[Route("api/crm")]
[Authorize]
public abstract class CRMControllerBase : ControllerBase
{
    protected readonly CRMDbContext DbContext;
    protected readonly IHttpClientFactory HttpClient;
    protected readonly IAgencyRepository AgencyRepository;
    protected readonly IBrokerRepository BrokerRepository;
    protected readonly ILeadRepository LeadRepository;
    protected readonly IDealRepository DealRepository;
    protected readonly ICompanyRepository CompanyRepository;
    protected readonly ILegalEntityRepository LegalEntityRepository;
    protected readonly IContactRepository ContactRepository;
    protected readonly IGeographyRepository GeographyRepository;
    protected readonly IOperationalRecordRepository OperationalRepository;
    protected readonly IBrochureCardRepository BrochureCardRepository;
    protected readonly IRepository<BrochureCardLine> BrochureCardLineRepository;
    protected readonly IDealBrokerSplitRepository DealBrokerSplitRepository;
    protected readonly INotificationRepository NotificationRepository;
    protected readonly IRepository<BrokerMonthlyTarget> BrokerTargetRepository;
    protected readonly PermissionCheckService PermissionCheck;
    protected readonly string GatewayUrl;
    protected readonly ILogger Logger;

    private const int PageSize = 20;

    protected CRMControllerBase(
        CRMDbContext dbContext,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger logger,
        PermissionCheckService permissionCheck,
        IAgencyRepository agencyRepository,
        IBrokerRepository brokerRepository,
        ILeadRepository leadRepository,
        IDealRepository dealRepository,
        ICompanyRepository companyRepository,
        ILegalEntityRepository legalEntityRepository,
        IContactRepository contactRepository,
        IGeographyRepository geographyRepository,
        IOperationalRecordRepository operationalRepository,
        IBrochureCardRepository brochureCardRepository,
        IRepository<BrochureCardLine> brochureCardLineRepository,
        IDealBrokerSplitRepository dealBrokerSplitRepository,
        INotificationRepository notificationRepository,
        IRepository<BrokerMonthlyTarget> brokerTargetRepository)
    {
        DbContext = dbContext;
        HttpClient = http;
        AgencyRepository = agencyRepository;
        BrokerRepository = brokerRepository;
        LeadRepository = leadRepository;
        DealRepository = dealRepository;
        CompanyRepository = companyRepository;
        LegalEntityRepository = legalEntityRepository;
        ContactRepository = contactRepository;
        GeographyRepository = geographyRepository;
        OperationalRepository = operationalRepository;
        BrochureCardRepository = brochureCardRepository;
        BrochureCardLineRepository = brochureCardLineRepository;
        DealBrokerSplitRepository = dealBrokerSplitRepository;
        NotificationRepository = notificationRepository;
        BrokerTargetRepository = brokerTargetRepository;
        GatewayUrl = configuration["Gateway:BaseUrl"]
            ?? throw new InvalidOperationException("Gateway:BaseUrl is not configured in appsettings.json.");
        Logger = logger;
        PermissionCheck = permissionCheck;
    }

    /// <summary>
    /// Resolves the caller's tenant directly from the bearer JWT claim
    /// instead of trusting a client-supplied URL parameter.
    /// </summary>
    protected string GetCurrentTenant()
    {
        var tenant = User?.FindFirst("TenantId")?.Value
                     ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
        return tenant ?? string.Empty;
    }

    /// <summary>
    /// Canonical role from the JWT (mirrors Identity normalization) for role-gating.
    /// </summary>
    protected string GetCurrentCanonicalRole()
    {
        var role = User?.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                   ?? User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return role switch
        {
            "SuperAdmin" => "PlatformAdmin",
            "Agent" => "Broker",
            "Manager" or "Admin" or "AgencyAdmin" => "AgencyAdmin",
            _ => role ?? string.Empty
        };
    }

    /// <summary>
    /// Returns the caller's user id (NameIdentifier claim) — the new
    /// identity key for AllocatedBrokerIds (comma-separated broker ids).
    /// </summary>
    protected string GetCurrentUserId() =>
        User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    /// <summary>
    /// Returns the caller's display name from the JWT Name claim.
    /// </summary>
    protected string GetCurrentUserName() =>
        User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;

    protected static readonly System.Text.RegularExpressions.Regex BrokerIdSplit =
        new(@"\s*,\s*", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// True when the caller's user id appears as a token in a
    /// comma-separated AllocatedBrokerIds string. Used by Broker-auth
    /// checks after the legacy single-name field was replaced.
    /// </summary>
    protected static bool CallerIsAssignedTo(string allocatedBrokerIds, string callerId)
    {
        if (string.IsNullOrWhiteSpace(callerId) || string.IsNullOrEmpty(allocatedBrokerIds))
            return false;
        foreach (var id in BrokerIdSplit.Split(allocatedBrokerIds))
            if (string.Equals(id, callerId, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// Helper: check if the given brokerId appears in the comma-separated
    /// AllocatedBrokerIds field. Used by the leaderboard + metrics.
    /// </summary>
    protected static bool BrokerMatches(string allocatedBrokerIds, string brokerId)
    {
        if (string.IsNullOrEmpty(allocatedBrokerIds)) return false;
        return allocatedBrokerIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(id => string.Equals(id, brokerId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the caller's tenant + all child-agency tenant IDs.
    ///   - PlatformAdmin: empty (caller sees everything via unfiltered query)
    ///   - AgencyAdmin: own tenant + any tenant whose AgencyRecord has ParentAgencyId
    ///     pointing into the caller's agency subtree.
    ///   - Other roles: only their own tenant.
    ///   - overrideAgencyId: when provided, narrows the returned list to the
    ///     TenantId of that AgencyRecord (must still be inside the caller's
    ///     visible subtree; the helper drops the override for callers that
    ///     aren't allowed to switch). "__all__" disables narrowing.
    /// </summary>
    protected async Task<List<string>> GetVisibleTenantIdsAsync(string? overrideAgencyId = null)
    {
        var tenant = GetCurrentTenant();
        var role = GetCurrentCanonicalRole();

        List<string> baseVisible;
        if (string.IsNullOrEmpty(tenant) || role == "PlatformAdmin")
        {
            if (role == "PlatformAdmin")
            {
                baseVisible = await AgencyRepository.Query()
                    .Select(a => a.TenantId)
                    .Where(t => t != null && t != "")
                    .Distinct()
                    .ToListAsync();
            }
            else
            {
                baseVisible = new List<string>();
            }
        }
        else
        {
            var visible = new HashSet<string> { tenant };

            if (role == "AgencyAdmin")
            {
                var callerAgencyIds = await AgencyRepository.Query()
                    .Where(a => a.TenantId == tenant)
                    .Select(a => a.AgencyId)
                    .ToListAsync();

                if (callerAgencyIds.Count == 0)
                {
                    return visible.ToList();
                }

                var frontier = new Queue<string>(callerAgencyIds);
                var visited = new HashSet<string>(callerAgencyIds);

                while (frontier.Count > 0)
                {
                    var batch = frontier.Dequeue();
                    var childAgencyIds = await AgencyRepository.Query()
                        .Where(a => a.ParentAgencyId == batch)
                        .Select(a => new { a.AgencyId, a.TenantId })
                        .ToListAsync();

                    foreach (var child in childAgencyIds)
                    {
                        if (!visited.Contains(child.AgencyId))
                        {
                            visited.Add(child.AgencyId);
                            if (!string.IsNullOrEmpty(child.TenantId))
                            {
                                visible.Add(child.TenantId);
                            }
                            frontier.Enqueue(child.AgencyId);
                        }
                    }
                }
            }

            baseVisible = visible.ToList();
        }

        if (string.IsNullOrWhiteSpace(overrideAgencyId) || overrideAgencyId == "__all__")
            return baseVisible;

        var overrideTenants = await ResolveOverrideSubtreeAsync(overrideAgencyId, role);
        if (overrideTenants == null)
        {
            Logger.LogWarning("Tenant override unresolved: agencyId {Override} for role {Role}",
                overrideAgencyId, role);
            return [];
        }
        return overrideTenants.Intersect(baseVisible, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Resolves the overrideAgencyId (from the TenantSwitcher dropdown)
    /// into the full list of TenantIds the request should be scoped to,
    /// applying the parent-vs-child subtree rule.
    /// </summary>
    protected async Task<List<string>?> ResolveOverrideSubtreeAsync(string overrideAgencyId, string role)
    {
        var agency = await AgencyRepository.Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgencyId == overrideAgencyId);

        AgencyRecord? target = agency;
        if (target == null)
        {
            target = await AgencyRepository.Query()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.TenantId == overrideAgencyId);
        }
        if (target == null || string.IsNullOrEmpty(target.TenantId)) return null;

        var hasParent = !string.IsNullOrWhiteSpace(target.ParentAgencyId);
        if (!hasParent)
        {
            var subtree = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { target.TenantId };
            var frontier = new Queue<string>();
            frontier.Enqueue(target.AgencyId);
            while (frontier.Count > 0)
            {
                var batch = frontier.Dequeue();
                var childAgencyIds = await AgencyRepository.Query()
                    .AsNoTracking()
                    .Where(a => a.ParentAgencyId == batch)
                    .Select(a => a.AgencyId)
                    .ToListAsync();

                foreach (var childId in childAgencyIds)
                {
                    if (string.IsNullOrEmpty(childId)) continue;
                    var childAgency = await AgencyRepository.Query()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.AgencyId == childId);
                    if (childAgency == null) continue;
                    if (!string.IsNullOrEmpty(childAgency.TenantId) && subtree.Add(childAgency.TenantId))
                    {
                        frontier.Enqueue(childAgency.AgencyId);
                    }
                }
            }
            Logger.LogInformation("Tenant override subtree: picked top-level agency {AgencyId} ({TenantId}) → {Count} tenants",
                target.AgencyId, target.TenantId, subtree.Count);
            return subtree.ToList();
        }

        Logger.LogInformation("Tenant override leaf: picked child agency {AgencyId} ({TenantId}) → 1 tenant",
            target.AgencyId, target.TenantId);
        return new List<string> { target.TenantId };
    }

    /// <summary>
    /// Returns the 1-based page number from a query parameter, defaulting to 1.
    /// </summary>
    protected int GetPage([FromQuery] int page) => page > 0 ? page : 1;

    /// <summary>
    /// Server-side pagination helper: returns the page slice + total count.
    /// (total is exposed via the X-Total-Count response header for infinite scroll).
    /// </summary>
    protected async Task<(List<T> Items, int Total)> PageAsync<T>(IQueryable<T> query, int page)
    {
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return (items, total);
    }

    /// <summary>
    /// String-typed selector aggregate: groups rows by a string field and
    /// returns { bucket → count }.
    /// </summary>
    protected static async Task<Dictionary<string, int>> AggregateStringAsync<T>(
        IEnumerable<T> source,
        System.Linq.Expressions.Expression<Func<T, string?>> selector)
    {
        var compiled = selector.Compile();
        var rows = source
            .GroupBy(compiled)
            .Select(g => new { Key = g.Key ?? "(Unset)", Count = g.Count() })
            .ToList();
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) dict[row.Key] = row.Count;
        return dict;
    }

    /// <summary>
    /// Boolean-typed selector aggregate: groups rows by a bool field and
    /// returns { true/false → count }.
    /// </summary>
    protected static async Task<Dictionary<string, int>> AggregateBoolAsync<T>(
        IEnumerable<T> source,
        System.Linq.Expressions.Expression<Func<T, bool>> selector)
    {
        var compiled = selector.Compile();
        var rows = source
            .GroupBy(compiled)
            .Select(g => new { Key = g.Key ? "true" : "false", Count = g.Count() })
            .ToList();
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) dict[row.Key] = row.Count;
        return dict;
    }

    /// <summary>
    /// Pack an aggregate dictionary into a JSON header value (URL-safe).
    /// </summary>
    protected static string SerializeAggregates(Dictionary<string, Dictionary<string, int>> aggregates)
    {
        if (aggregates == null || aggregates.Count == 0) return string.Empty;
        return JsonSerializer.Serialize(aggregates);
    }
}
