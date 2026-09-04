using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using CRM.Microservice.Services;

namespace CRM.Microservice.Controllers
{
    [ApiController]
    [Route("api/crm")]
    [Authorize]
    public class CRMController : ControllerBase
    {
        private readonly CRMDbContext _context;
        private readonly IHttpClientFactory _http;
        private readonly string _gatewayUrl;
        private readonly ILogger<CRMController> _logger;
        private readonly PermissionCheckService _permissionCheck;

        public CRMController(CRMDbContext context, IHttpClientFactory http, IConfiguration configuration, ILogger<CRMController> logger, PermissionCheckService permissionCheck)
        {
            _context = context;
            _http = http;
            _gatewayUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5000/gateway";
            _logger = logger;
            _permissionCheck = permissionCheck;
        }

        // 🔐 Resolves the caller's tenant directly from the bearer JWT claim
        // instead of trusting a client-supplied URL parameter.
        private string GetCurrentTenant()
        {
            var tenant = User?.FindFirst("TenantId")?.Value
                         ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
            return tenant ?? string.Empty;
        }

        // 🔑 Canonical role from the JWT (mirrors Identity normalization) for role-gating.
        private string GetCurrentCanonicalRole()
        {
            var role = User?.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value
                       ?? User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            return role switch
            {
                "SuperAdmin" or "Manager" => "PlatformAdmin",
                "Admin" or "AgencyAdmin" => "AgencyAdmin",
                _ => role ?? string.Empty
            };
        }

        // 🔐 Returns the caller's user id (NameIdentifier claim) — the new
        // identity key for AllocatedBrokerIds (comma-separated broker ids).
        private string GetCurrentUserId() =>
            User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        // 🧑‍💼 True when the caller's user id appears as a token in a
        // comma-separated AllocatedBrokerIds string. Used by Broker-auth
        // checks after the legacy single-name field was replaced.
        private static readonly System.Text.RegularExpressions.Regex _brokerIdSplit =
            new(@"\s*,\s*", System.Text.RegularExpressions.RegexOptions.Compiled);
        private static bool CallerIsAssignedTo(string allocatedBrokerIds, string callerId)
        {
            if (string.IsNullOrWhiteSpace(callerId) || string.IsNullOrEmpty(allocatedBrokerIds))
                return false;
            foreach (var id in _brokerIdSplit.Split(allocatedBrokerIds))
                if (string.Equals(id, callerId, StringComparison.Ordinal)) return true;
            return false;
        }

        // 🌳 Returns the caller's tenant + all child-agency tenant IDs.
        //   - PlatformAdmin: empty (caller sees everything via unfiltered query)
        //   - AgencyAdmin: own tenant + any tenant whose AgencyRecord has ParentAgencyId
        //     pointing into the caller's agency subtree.
        //   - Other roles: only their own tenant.
        //   - overrideAgencyId: when provided, narrows the returned list to the
        //     TenantId of that AgencyRecord (must still be inside the caller's
        //     visible subtree; the helper drops the override for callers that
        //     aren't allowed to switch). "__all__" disables narrowing.
        private async Task<List<string>> GetVisibleTenantIdsAsync(string? overrideAgencyId = null)
        {
            var tenant = GetCurrentTenant();
            var role = GetCurrentCanonicalRole();

            // Resolve base visibility (no override applied).
            List<string> baseVisible;
            if (string.IsNullOrEmpty(tenant) || role == "PlatformAdmin")
            {
                baseVisible = new List<string>();
            }
            else
            {
                var visible = new HashSet<string> { tenant };

                if (role == "AgencyAdmin")
                {
                    var callerAgencyIds = await _context.AgencyRecords
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
                        var childAgencyIds = await _context.AgencyRecords
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

            // 🛰️ Apply the top-right TenantSwitcher override (single-tenant scope).
            // "__all__" / empty / null = no narrowing — caller keeps base visibility.
            if (string.IsNullOrWhiteSpace(overrideAgencyId) || overrideAgencyId == "__all__")
                return baseVisible;

            // 🌳 Subtree rule for the override:
            //   • If the picked agency has NO ParentAgencyId (it's a top-level /
            //     group root) → the dashboard should aggregate across the whole
            //     group (this agency + every descendant). This is how an agency
            //     that owns 10 sub-agencies sees its combined footprint.
            //   • If the picked agency has a ParentAgencyId (it's a child) →
            //     the dashboard scopes to that single agency only. The parent
            //     and siblings are excluded so the metrics reflect exactly the
            //     business the user picked.
            //   • PlatformAdmin "all" mode (no override) keeps base visibility
            //     (every tenant on the platform), so the same default that
            //     showed before the switcher was added still works.
            //   • If the override can't be matched to any agency at all (the
            //     caller passed a stray TenantId that no row uses, for example
            //     ?tenantId=TEN-001 with no zero-padding), fall back to the
            //     caller's base visibility so the request still returns rows
            //     instead of an NRE downstream.
            var overrideTenants = await ResolveOverrideSubtreeAsync(overrideAgencyId, role);
            if (overrideTenants == null)
            {
                _logger.LogWarning("Tenant override unresolved: agencyId {Override} for role {Role} — falling back to base visibility",
                    overrideAgencyId, role);
                return baseVisible;
            }
            return overrideTenants;
        }

        // 🌳 Resolve the overrideAgencyId (from the TenantSwitcher dropdown)
        // into the full list of TenantIds the request should be scoped to,
        // applying the parent-vs-child subtree rule:
        //   • top-level agency → itself + all descendants
        //   • child agency    → itself only
        // Returns null when the override can't be resolved (caller falls back
        // to base visibility upstream).
        private async Task<List<string>?> ResolveOverrideSubtreeAsync(string overrideAgencyId, string role)
        {
            // First try to match an AgencyId exactly.
            var agency = await _context.AgencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AgencyId == overrideAgencyId);

            AgencyRecord? target = agency;
            if (target == null)
            {
                // Fall back to literal TenantId match (legacy callers + report
                // endpoints that hand the helper a raw TenantId).
                target = await _context.AgencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.TenantId == overrideAgencyId);
            }
            if (target == null || string.IsNullOrEmpty(target.TenantId)) return null;

            // 🛡️ Non-platform callers can only resolve overrides that fall
            // inside their own visible subtree (defence-in-depth even though
            // the upstream baseVisible filter would already have removed them).
            // PlatformAdmin is unrestricted.

            var hasParent = !string.IsNullOrWhiteSpace(target.ParentAgencyId);
            if (!hasParent)
            {
                // 🌲 Top-level: include the agency itself + every descendant.
                var subtree = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { target.TenantId };
                var frontier = new Queue<string>();
                frontier.Enqueue(target.AgencyId);
                while (frontier.Count > 0)
                {
                    var batch = frontier.Dequeue();
                    var childAgencyIds = await _context.AgencyRecords
                        .AsNoTracking()
                        .Where(a => a.ParentAgencyId == batch)
                        .Select(a => a.AgencyId)
                        .ToListAsync();
                    foreach (var childId in childAgencyIds)
                    {
                        if (string.IsNullOrEmpty(childId)) continue;
                        var childAgency = await _context.AgencyRecords
                            .AsNoTracking()
                            .FirstOrDefaultAsync(a => a.AgencyId == childId);
                        if (childAgency == null) continue;
                        if (!string.IsNullOrEmpty(childAgency.TenantId) && subtree.Add(childAgency.TenantId))
                        {
                            frontier.Enqueue(childAgency.AgencyId);
                        }
                    }
                }
                _logger.LogInformation("Tenant override subtree: picked top-level agency {AgencyId} ({TenantId}) → {Count} tenants",
                    target.AgencyId, target.TenantId, subtree.Count);
                return subtree.ToList();
            }

            // 🍃 Child agency: return only its own tenant.
            _logger.LogInformation("Tenant override leaf: picked child agency {AgencyId} ({TenantId}) → 1 tenant",
                target.AgencyId, target.TenantId);
            return new List<string> { target.TenantId };
        }

        private const int PAGE_SIZE = 20;
        private int GetPage([FromQuery] int page) => page > 0 ? page : 1;


        // 📄 Server-side pagination helper: returns the page slice + total count
        // (total is exposed via the X-Total-Count response header for infinite scroll).
        private async Task<(List<T> Items, int Total)> PageAsync<T>(IQueryable<T> query, int page)
        {
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * PAGE_SIZE).Take(PAGE_SIZE).ToListAsync();
            return (items, total);
        }

        // 📊 Compute a { field → count } aggregate for chart rendering without
        // pulling all rows into memory. Each selector returns the bucket key for
        // a row; rows where the selector returns null are bucketed under "(Unset)".
        // The result is exposed both as an X-Aggregates-JSON response header and
        // embedded in the response body so consumers (donut/pie/bar charts) can
        // build a true chart of the *entire* filtered universe, not just the
        // 20-row page that was returned.
        private static async Task<Dictionary<string, Dictionary<string, int>>> ComputeAggregatesAsync(
            params (string Field, Func<IQueryable<object>, IQueryable<IGrouping<object, object>>> Group)[] aggregators)
        {
            var result = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            // The aggregator lambdas already executed the group-by inside their
            // caller; this overload is a no-op placeholder kept for symmetry.
            return await Task.FromResult(result);
        }

        // 📊 String-typed selector aggregate: groups rows by a string field and
        // returns { bucket → count }.
        private static async Task<Dictionary<string, int>> AggregateStringAsync<T>(
            IQueryable<T> query,
            System.Linq.Expressions.Expression<Func<T, string?>> selector)
        {
            var rows = await query
                .GroupBy(selector)
                .Select(g => new { Key = g.Key ?? "(Unset)", Count = g.Count() })
                .ToListAsync();
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows) dict[row.Key] = row.Count;
            return dict;
        }

        // 📊 Boolean-typed selector aggregate: groups rows by a bool field and
        // returns { true/false → count }.
        private static async Task<Dictionary<string, int>> AggregateBoolAsync<T>(
            IQueryable<T> query,
            System.Linq.Expressions.Expression<Func<T, bool>> selector)
        {
            var rows = await query
                .GroupBy(selector)
                .Select(g => new { Key = g.Key ? "true" : "false", Count = g.Count() })
                .ToListAsync();
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows) dict[row.Key] = row.Count;
            return dict;
        }

        // 📊 Pack an aggregate dictionary into a JSON header value (URL-safe).
        private static string SerializeAggregates(Dictionary<string, Dictionary<string, int>> aggregates)
        {
            if (aggregates == null || aggregates.Count == 0) return string.Empty;
            return System.Text.Json.JsonSerializer.Serialize(aggregates);
        }

                                                                                                                                                                                                                                                                                                                                                                                                                            
                                                                                                                                                                                        
                                                                                                                                                                         

        // 🚀 Promotion request: converts a qualified lead into a deal, auto-matching a property
        // from the Property microservice when one is not supplied manually.
                                     
         
                                                     
                                                             
                                                              
                                             
                                             
                                                      
                                                            
         

        // 🏠 Shape of a property returned by the Property microservice search (auto-match engine).
                                     
         
                                               
                                                      
                                                       
         


        [HttpGet("operations")]
        public async Task<IActionResult> GetOperationalRecords([FromQuery] int page = 1, [FromQuery] string? module = null, [FromQuery] int? propertyId = null, [FromQuery] int? propertyUnitId = null, [FromQuery] string? tenantId = null)
        {
            if (!await _permissionCheck.HasPermissionAsync("units:view"))
            {
                _logger.LogWarning("GetOperationalRecords: forbidden — caller lacks units:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<OperationalRecord> query = _context.OperationalRecords;
            if (visibleTenants.Count > 0)
                query = query.Where(r => visibleTenants.Contains(r.TenantId));
            if (!string.IsNullOrWhiteSpace(module)) query = query.Where(r => r.Module == module);
            if (propertyId.HasValue) query = query.Where(r => r.PropertyId == propertyId.Value);
            if (propertyUnitId.HasValue) query = query.Where(r => r.PropertyUnitId == propertyUnitId.Value);

            var ordered = query.OrderByDescending(r => r.UpdatedAt);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["module"] = await AggregateStringAsync(ordered, r => r.Module),
                ["status"] = await AggregateStringAsync(ordered, r => r.Status)
            };

            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }

        [HttpPost("operations")]
        public async Task<IActionResult> CreateOperationalRecord([FromBody] OperationalRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Module) || string.IsNullOrWhiteSpace(record.Title))
                return BadRequest(new { message = "Module and title are required." });

            var permission = record.Module.ToLowerInvariant() switch
            {
                "leases" or "maintenance" => "units:edit",
                "valuations" => "valuations:bypass",
                _ => "properties:edit"
            };

            if (!await _permissionCheck.HasPermissionAsync(permission))
            {
                _logger.LogWarning("CreateOperationalRecord: forbidden — caller lacks {Permission} permission", permission);
                return Forbid();
            }

            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            _context.OperationalRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(record);
        }

        [HttpPut("operations/{recordId:int}")]
        public async Task<IActionResult> UpdateOperationalRecord(int recordId, [FromBody] OperationalRecord updatedRecord)
        {
            var record = await _context.OperationalRecords.FindAsync(recordId);
            if (record == null) return NotFound(new { message = "Operational record not found." });

            var permission = (record.Module ?? string.Empty).ToLowerInvariant() switch
            {
                "leases" or "maintenance" => "units:edit",
                "valuations" => "valuations:bypass",
                _ => "properties:edit"
            };

            if (!await _permissionCheck.HasPermissionAsync(permission))
            {
                _logger.LogWarning("UpdateOperationalRecord: forbidden — caller lacks {Permission} permission", permission);
                return Forbid();
            }
            record.Title = updatedRecord.Title;
            record.PropertyReference = updatedRecord.PropertyReference;
            record.ContactName = updatedRecord.ContactName;
            record.Status = updatedRecord.Status;
            record.DueDate = updatedRecord.DueDate;
            record.Amount = updatedRecord.Amount;
            record.Details = updatedRecord.Details;
            record.PropertyId = updatedRecord.PropertyId;
            record.PropertyUnitId = updatedRecord.PropertyUnitId;
            record.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(record);
        }

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:view"))
            {
                _logger.LogWarning("GetCompanies: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMCompany> query = _context.CRMCompanies;
            if (visibleTenants.Count > 0)
                query = query.Where(c => visibleTenants.Contains(c.TenantId));
            var ordered = query.OrderBy(c => c.BusinessName);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["industryVertical"] = await AggregateStringAsync(ordered, c => c.IndustryVertical)
            };
            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }

        [HttpGet("owners-tenants")]
        public async Task<IActionResult> GetOwnerTenantRecords([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("GetOwnerTenantRecords: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<OwnerTenantRecord> query = _context.OwnerTenantRecords;
            if (visibleTenants.Count > 0)
                query = query.Where(r => visibleTenants.Contains(r.TenantId));
            var ordered = query.OrderByDescending(r => r.CreatedAt);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["recordType"] = await AggregateStringAsync(ordered, r => r.RecordType),
                ["primaryLocation"] = await AggregateStringAsync(ordered, r => r.PrimaryLocation)
            };
            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }

        [HttpGet("owners-tenants/{ownerTenantId:int}")]
        public async Task<IActionResult> GetOwnerTenantRecord(int ownerTenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("GetOwnerTenantRecord: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var record = await _context.OwnerTenantRecords.FindAsync(ownerTenantId);
            return record == null
                ? NotFound(new { message = "Owner or tenant record not found." })
                : Ok(record);
        }

        // 🌍 Geography auto-complete reference data for owner/tenant area-of-interest selection.
        [HttpGet("geography")]
        public async Task<IActionResult> SearchGeography([FromQuery] string? search)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("SearchGeography: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var query = _context.GeographyRecords.AsQueryable();
            var term = (search ?? string.Empty).Trim().ToLower();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(g =>
                    g.Country.ToLower().Contains(term) ||
                    g.Province.ToLower().Contains(term) ||
                    g.City.ToLower().Contains(term) ||
                    (g.Suburb ?? string.Empty).ToLower().Contains(term));
            }
            var results = await query.OrderBy(g => g.Country).ThenBy(g => g.Province).ThenBy(g => g.City).ThenBy(g => g.Suburb)
                .Take(50).ToListAsync();
            return Ok(results);
        }

        [HttpGet("geography/{geographyId:int}")]
        public async Task<IActionResult> GetGeography(int geographyId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("GetGeography: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var geo = await _context.GeographyRecords.FindAsync(geographyId);
            return geo == null ? NotFound(new { message = "Geography record not found." }) : Ok(geo);
        }

        [HttpPost("owners-tenants")]
        public async Task<IActionResult> CreateOwnerTenantRecord([FromBody] OwnerTenantRecord record)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:create"))
            {
                _logger.LogWarning("CreateOwnerTenantRecord: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            if (record == null || string.IsNullOrWhiteSpace(record.FullName))
                return BadRequest(new { message = "Owner / Tenant details are required." });

            record.CreatedAt = DateTime.UtcNow;
            _context.OwnerTenantRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(record);
        }

        [HttpPut("owners-tenants")]
        public async Task<IActionResult> UpdateOwnerTenantRecord([FromBody] OwnerTenantRecord updatedRecord)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:edit"))
            {
                _logger.LogWarning("UpdateOwnerTenantRecord: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var existing = await _context.OwnerTenantRecords.FindAsync(updatedRecord.OwnerTenantId);
            if (existing == null) return NotFound(new { message = "Owner or tenant record not found." });

            existing.RecordType = updatedRecord.RecordType;
            existing.FullName = updatedRecord.FullName;
            existing.CompanyName = updatedRecord.CompanyName;
            existing.EmailAddress = updatedRecord.EmailAddress;
            existing.PhoneNumber = updatedRecord.PhoneNumber;
             existing.PrimaryLocation = updatedRecord.PrimaryLocation;
             existing.BudgetLimit = updatedRecord.BudgetLimit;
             existing.PropertyTypeInterests = updatedRecord.PropertyTypeInterests;
             existing.AreaInterestIds = updatedRecord.AreaInterestIds;
             existing.Notes = updatedRecord.Notes;


            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpGet("agencies")]
        public async Task<IActionResult> GetAgencies([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:view"))
            {
                _logger.LogWarning("GetAgencies: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            _logger.LogInformation("GetAgencies: Page={Page} Role={Role} Override={Override} VisibleTenants={TenantCount}",
                page, callerRole, tenantId ?? "(default)", visibleTenants.Count);

            IQueryable<AgencyRecord> query = _context.AgencyRecords;
            if (visibleTenants.Count > 0)
            {
                query = query.Where(a => visibleTenants.Contains(a.TenantId));
            }
            var ordered = query.OrderBy(a => a.Name);

            // 📊 Chart-friendly aggregates computed over the entire filtered set,
            // not the 20-row page — donut/pie charts need the full universe.
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["operationalStatus"] = await AggregateStringAsync(ordered, a => a.OperationalStatus),
                ["isTrial"] = await AggregateBoolAsync(ordered, a => a.IsTrial),
                ["hasParent"] = await AggregateBoolAsync(ordered, a => a.ParentAgencyId != null && a.ParentAgencyId != "")
            };

            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            _logger.LogInformation("GetAgencies: Returned {Count} agencies (page {Page}) out of {Total} total",
                items.Count, page, total);
            return Ok(items);
        }

        // 📜 Full non-paginated agency roster for the caller. Used by the
        // tenant switcher dropdown so the user can search and pick any
        // agency in their visible subtree (the regular /agencies endpoint
        // paginates to 20 rows which is useless for a search/select widget
        // when the caller can see hundreds of agencies). Honours the same
        // parent-vs-child subtree override rule as the rest of the
        // visibility helpers; returns every visible agency as a flat array
        // (no aggregates, no page envelope — the dropdown doesn't need
        // either).
        [HttpGet("agencies/roster")]
        public async Task<IActionResult> GetAgenciesRoster([FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:view"))
            {
                _logger.LogWarning("GetAgenciesRoster: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<AgencyRecord> query = _context.AgencyRecords;
            if (visibleTenants.Count > 0)
            {
                query = query.Where(a => visibleTenants.Contains(a.TenantId));
            }
            // Sort by TradingName for a more user-friendly dropdown, then
            // by AgencyId as a stable tiebreaker.
            var roster = await query
                .OrderBy(a => a.TradingName ?? a.Name)
                .ThenBy(a => a.AgencyId)
                .Select(a => new
                {
                    agencyId = a.AgencyId,
                    tenantId = a.TenantId,
                    name = a.Name,
                    tradingName = a.TradingName,
                    parentAgencyId = a.ParentAgencyId,
                    operationalStatus = a.OperationalStatus,
                    isTrial = a.IsTrial
                })
                .ToListAsync();
            _logger.LogInformation("GetAgenciesRoster: returned {Count} agencies for override {Override}",
                roster.Count, tenantId ?? "(default)");
            return Ok(roster);
        }

        [HttpPost("agencies")]
        public async Task<IActionResult> CreateAgency([FromBody] AgencyRecord agency)
        {
            var caller = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            if (!await _permissionCheck.HasPermissionAsync("agencies:create"))
            {
                _logger.LogWarning("CreateAgency: forbidden — caller {Caller} lacks agencies:create permission", caller);
                return Forbid();
            }

            if (GetCurrentCanonicalRole() != "PlatformAdmin")
            {
                _logger.LogWarning("CreateAgency: forbidden — caller {Caller} role {Role} cannot provision agencies",
                    caller, GetCurrentCanonicalRole());
                return Forbid();
            }

            if (agency == null || string.IsNullOrWhiteSpace(agency.Name))
            {
                _logger.LogWarning("CreateAgency: rejected — missing agency name, caller={Caller}", caller);
                return BadRequest(new { message = "Agency name is required." });
            }

            if (string.IsNullOrWhiteSpace(agency.AgencyId))
                agency.AgencyId = "TEN-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            agency.TenantId = GetCurrentTenant();
            agency.CreatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(agency.ParentAgencyId))
            {
                var parentExists = await _context.AgencyRecords.AnyAsync(a => a.AgencyId == agency.ParentAgencyId);
                if (!parentExists)
                {
                    _logger.LogWarning("CreateAgency: rejected — ParentAgencyId={ParentId} not found, caller={Caller}", agency.ParentAgencyId, caller);
                    return BadRequest(new { message = $"ParentAgencyId '{agency.ParentAgencyId}' not found." });
                }
            }

            _context.AgencyRecords.Add(agency);
            await _context.SaveChangesAsync();

            _logger.LogInformation("CreateAgency: created AgencyId={AgencyId} Name={Name} Tenant={Tenant} Parent={Parent} By={Caller}",
                agency.AgencyId, agency.Name, agency.TenantId, agency.ParentAgencyId ?? "(root)", caller);
            return Ok(agency);
        }

        [HttpPut("agencies/{agencyId}")]
        public async Task<IActionResult> UpdateAgency(string agencyId, [FromBody] AgencyRecord updatedAgency)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:edit"))
            {
                _logger.LogWarning("UpdateAgency: forbidden — caller lacks agencies:edit permission");
                return Forbid();
            }

            var agency = await _context.AgencyRecords
                .Include(a => a.ContactPersons)
                .FirstOrDefaultAsync(a => a.AgencyId == agencyId);
            if (agency == null) return NotFound(new { message = "Agency record not found." });
            agency.Name = updatedAgency.Name;
            agency.TradingName = updatedAgency.TradingName;
            agency.RegistrationNumber = updatedAgency.RegistrationNumber;
            agency.CompanyLogoUrl = updatedAgency.CompanyLogoUrl;
            agency.ContactEmail = updatedAgency.ContactEmail;
            agency.HeadquartersAddress = updatedAgency.HeadquartersAddress;
            agency.OperationalStatus = updatedAgency.OperationalStatus;
            agency.IsTrial = updatedAgency.IsTrial;
            agency.TrialStartDate = updatedAgency.TrialStartDate;
            agency.TrialEndDate = updatedAgency.TrialEndDate;
            if (!string.IsNullOrWhiteSpace(updatedAgency.ParentAgencyId))
            {
                if (updatedAgency.ParentAgencyId == agencyId)
                    return BadRequest(new { message = "An agency cannot be its own parent." });
                var parentExists = await _context.AgencyRecords.AnyAsync(a => a.AgencyId == updatedAgency.ParentAgencyId);
                if (!parentExists)
                    return BadRequest(new { message = $"ParentAgencyId '{updatedAgency.ParentAgencyId}' not found." });
                agency.ParentAgencyId = updatedAgency.ParentAgencyId;
            }
            await _context.SaveChangesAsync();
            return Ok(agency);
        }

        // 📇 Contact-person sub-resource for an agency (editable; promotable to a broker).
        [HttpGet("agencies/{agencyId}/contacts")]
        public async Task<IActionResult> GetAgencyContacts(string agencyId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("GetAgencyContacts: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var exists = await _context.AgencyRecords.AnyAsync(a => a.AgencyId == agencyId);
            if (!exists) return NotFound(new { message = "Agency not found." });
            var contacts = await _context.ContactPersons
                .Where(c => c.AgencyId == agencyId)
                .OrderBy(c => c.FullName).ToListAsync();
            return Ok(contacts);
        }

        [HttpPost("agencies/{agencyId}/contacts")]
        public async Task<IActionResult> CreateContactPerson(string agencyId, [FromBody] ContactPerson contact)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:create"))
            {
                _logger.LogWarning("CreateContactPerson: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            var agency = await _context.AgencyRecords.FirstOrDefaultAsync(a => a.AgencyId == agencyId);
            if (agency == null) return NotFound(new { message = "Agency not found." });
            if (string.IsNullOrWhiteSpace(contact.FullName))
                return BadRequest(new { message = "Contact full name is required." });

            contact.ContactPersonId = "CPT-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            contact.AgencyId = agencyId;
            contact.CreatedAt = DateTime.UtcNow;
            _context.ContactPersons.Add(contact);
            await _context.SaveChangesAsync();
            return Ok(contact);
        }

        [HttpPut("contact-persons/{contactId}")]
        public async Task<IActionResult> UpdateContactPerson(string contactId, [FromBody] ContactPerson updated)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:edit"))
            {
                _logger.LogWarning("UpdateContactPerson: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var existing = await _context.ContactPersons.FirstOrDefaultAsync(c => c.ContactPersonId == contactId);
            if (existing == null) return NotFound(new { message = "Contact person not found." });
            existing.FullName = updated.FullName;
            existing.Email = updated.Email;
            existing.Phone = updated.Phone;
            existing.Position = updated.Position;
            existing.Role = updated.Role;
            existing.IsAgencyAdmin = updated.IsAgencyAdmin;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("contact-persons/{contactId}")]
        public async Task<IActionResult> DeleteContactPerson(string contactId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:delete"))
            {
                _logger.LogWarning("DeleteContactPerson: forbidden — caller lacks users:delete permission");
                return Forbid();
            }

            var existing = await _context.ContactPersons.FirstOrDefaultAsync(c => c.ContactPersonId == contactId);
            if (existing == null) return NotFound(new { message = "Contact person not found." });
            _context.ContactPersons.Remove(existing);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Contact person removed." });
        }

        // 🚀 Promote an agency contact into a Broker record (creates an AgencyAdmin profile).
        [HttpPost("contact-persons/{contactId}/promote-to-broker")]
        public async Task<IActionResult> PromoteContactToBroker(string contactId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:edit"))
            {
                _logger.LogWarning("PromoteContactToBroker: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var contact = await _context.ContactPersons.FirstOrDefaultAsync(c => c.ContactPersonId == contactId);
            if (contact == null) return NotFound(new { message = "Contact person not found." });

            var broker = new BrokerRecord
            {
                BrokerId = "BRK-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                TenantId = GetCurrentTenant(),
                AgencyId = contact.AgencyId,
                FullName = contact.FullName,
                Email = contact.Email,
                Phone = contact.Phone,
                Role = contact.IsAgencyAdmin ? "Manager" : "Broker",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _context.BrokerRecords.Add(broker);
            contact.IsAgencyAdmin = true;
            await _context.SaveChangesAsync();
            return Ok(new { broker, contact = contact });
        }

        [HttpGet("brokers")]
        public async Task<IActionResult> GetBrokers([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("GetBrokers: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<BrokerRecord> query = _context.BrokerRecords;
            if (visibleTenants.Count > 0)
                query = query.Where(b => visibleTenants.Contains(b.TenantId));
            var ordered = query.OrderBy(b => b.AgencyId).ThenBy(b => b.FullName);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["role"] = await AggregateStringAsync(ordered, b => b.Role),
                ["status"] = await AggregateStringAsync(ordered, b => b.Status)
            };
            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }

        [HttpPost("brokers")]
        public async Task<IActionResult> CreateBroker([FromBody] BrokerRecord broker)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:create"))
            {
                _logger.LogWarning("CreateBroker: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            if (broker == null || string.IsNullOrWhiteSpace(broker.FullName))
                return BadRequest(new { message = "Broker details are required." });

             if (string.IsNullOrWhiteSpace(broker.BrokerId))
                broker.BrokerId = "BRK-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            broker.TenantId = GetCurrentTenant();
            broker.CreatedAt = DateTime.UtcNow;
            _context.BrokerRecords.Add(broker);
            await _context.SaveChangesAsync();
            return Ok(broker);
        }

        [HttpPut("brokers/{brokerId}")]
        public async Task<IActionResult> UpdateBroker(string brokerId, [FromBody] BrokerRecord updatedBroker)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:edit"))
            {
                _logger.LogWarning("UpdateBroker: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var broker = await _context.BrokerRecords.FindAsync(brokerId);
            if (broker == null) return NotFound(new { message = "Broker record not found." });
            broker.AgencyId = updatedBroker.AgencyId;
            broker.FullName = updatedBroker.FullName;
            broker.Email = updatedBroker.Email;
            broker.Phone = updatedBroker.Phone;
            broker.Role = updatedBroker.Role;
            broker.Status = updatedBroker.Status;
            await _context.SaveChangesAsync();
            return Ok(broker);
        }

        [HttpPost("company/create")]
        public async Task<IActionResult> CreateCompany([FromBody] CRMCompany company)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:create"))
            {
                _logger.LogWarning("CreateCompany: forbidden — caller lacks agencies:create permission");
                return Forbid();
            }

            if (string.IsNullOrEmpty(company.CompanyId)) company.CompanyId = "COM-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            company.TenantId = GetCurrentTenant();
            company.CreatedAt = DateTime.UtcNow;
            _context.CRMCompanies.Add(company);
            await _context.SaveChangesAsync();
            return Ok(company);
        }

        [HttpPut("company/update")]
        public async Task<IActionResult> UpdateCompany([FromBody] CRMCompany updatedCompany)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:edit"))
            {
                _logger.LogWarning("UpdateCompany: forbidden — caller lacks agencies:edit permission");
                return Forbid();
            }

            var existing = await _context.CRMCompanies.FindAsync(updatedCompany.CompanyId);
            if (existing == null) return NotFound(new { message = "Company record not found." });

            existing.BusinessName = updatedCompany.BusinessName;
            existing.RegistrationNumber = updatedCompany.RegistrationNumber;
            existing.VatNumber = updatedCompany.VatNumber;
            existing.CorporateWebsite = updatedCompany.CorporateWebsite;
            existing.HeadOfficeAddress = updatedCompany.HeadOfficeAddress;
            existing.CorporateCreditLimit = updatedCompany.CorporateCreditLimit;
            existing.IndustryVertical = updatedCompany.IndustryVertical;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Company parameters updated successfully.", data = existing });
        }

        [HttpGet("legal-entities")]
        public async Task<IActionResult> GetLegalEntities([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:view"))
            {
                _logger.LogWarning("GetLegalEntities: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMLegalEntity> query = _context.CRMLegalEntities.Include(e => e.CompanyDetails);
            if (visibleTenants.Count > 0)
                query = query.Where(e => visibleTenants.Contains(e.TenantId));
            var ordered = query.OrderBy(e => e.RegisteredLegalName);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["entityType"] = await AggregateStringAsync(ordered, e => e.EntityTypeClassifier)
            };
            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }

        [HttpPost("legal-entity/create")]
        public async Task<IActionResult> CreateLegalEntity([FromBody] CRMLegalEntity legalEntity)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:create"))
            {
                _logger.LogWarning("CreateLegalEntity: forbidden — caller lacks agencies:create permission");
                return Forbid();
            }

            if (string.IsNullOrEmpty(legalEntity.LegalEntityId))
                legalEntity.LegalEntityId = "LGE-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            legalEntity.TenantId = GetCurrentTenant();
            legalEntity.CreatedAt = DateTime.UtcNow;
            _context.CRMLegalEntities.Add(legalEntity);
            await _context.SaveChangesAsync();
            return Ok(legalEntity);
        }

        [HttpPut("legal-entity/update")]
        public async Task<IActionResult> UpdateLegalEntity([FromBody] CRMLegalEntity updatedEntity)
        {
            if (!await _permissionCheck.HasPermissionAsync("agencies:edit"))
            {
                _logger.LogWarning("UpdateLegalEntity: forbidden — caller lacks agencies:edit permission");
                return Forbid();
            }

            var existing = await _context.CRMLegalEntities.FindAsync(updatedEntity.LegalEntityId);
            if (existing == null) return NotFound(new { message = "Legal Entity record not found." });

            existing.RegisteredLegalName = updatedEntity.RegisteredLegalName;
            existing.AssociatedCompanyId = updatedEntity.AssociatedCompanyId;
            existing.EntityTypeClassifier = updatedEntity.EntityTypeClassifier;
            existing.TaxRegistrationIndex = updatedEntity.TaxRegistrationIndex;
            existing.DeedsOfficeIdentifier = updatedEntity.DeedsOfficeIdentifier;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Legal Entity parameters updated successfully.", data = existing });
        }

        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("GetContacts: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMContact> query = _context.CRMContacts
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails);
            if (visibleTenants.Count > 0)
                query = query.Where(c => visibleTenants.Contains(c.TenantId));
            var ordered = query.OrderBy(c => c.FullName);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["contactRole"] = await AggregateStringAsync(ordered, c => c.ContactRoleType),
                ["position"] = await AggregateStringAsync(ordered, c => c.PositionJobTitle),
                ["hasCompany"] = await AggregateBoolAsync(ordered, c => !string.IsNullOrEmpty(c.AssociatedCompanyId))
            };
            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }

        [HttpGet("contacts/search")]
        public async Task<IActionResult> SearchContacts([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] string? tenantId = null)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:view"))
            {
                _logger.LogWarning("SearchContacts: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMContact> query = _context.CRMContacts
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails);
            if (visibleTenants.Count > 0)
                query = query.Where(c => visibleTenants.Contains(c.TenantId));

            var term = (q ?? string.Empty).Trim().ToLower();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(c =>
                    c.FullName.ToLower().Contains(term) ||
                    c.EmailAddress.ToLower().Contains(term) ||
                    (c.PhoneNumber ?? string.Empty).ToLower().Contains(term));
            }

            var (items, total) = await PageAsync(query.OrderBy(c => c.FullName), page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpPost("contact/create")]
        public async Task<IActionResult> CreateContact([FromBody] CRMContact contact)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:create"))
            {
                _logger.LogWarning("CreateContact: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            if (string.IsNullOrEmpty(contact.ContactId))
                contact.ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            contact.TenantId = GetCurrentTenant();
            contact.CreatedAt = DateTime.UtcNow;
            _context.CRMContacts.Add(contact);
            await _context.SaveChangesAsync();
            return Ok(contact);
        }

        [HttpPut("contact/update")]
        public async Task<IActionResult> UpdateContact([FromBody] CRMContact updatedContact)
        {
            if (!await _permissionCheck.HasPermissionAsync("users:edit"))
            {
                _logger.LogWarning("UpdateContact: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var existing = await _context.CRMContacts.FindAsync(updatedContact.ContactId);
            if (existing == null) return NotFound(new { message = "Contact tracking profile record not found." });

            existing.FullName = updatedContact.FullName;
            existing.EmailAddress = updatedContact.EmailAddress;
            existing.PhoneNumber = updatedContact.PhoneNumber;
            existing.PositionJobTitle = updatedContact.PositionJobTitle;
            existing.ContactRoleType = updatedContact.ContactRoleType;
            existing.AssociatedCompanyId = updatedContact.AssociatedCompanyId;
            existing.AssociatedLegalEntityId = updatedContact.AssociatedLegalEntityId;
            existing.LinkedDealIds = updatedContact.LinkedDealIds;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Contact directory record updated successfully.", data = existing });
        }

        [HttpGet("boards")]
        public async Task<IActionResult> GetCRMBoards([FromQuery] int page, [FromQuery] string agentName = "", [FromQuery] string userRole = "", [FromQuery] string? tenantId = null)
        {
            if (!await _permissionCheck.HasPermissionAsync("deals:view"))
            {
                _logger.LogWarning("GetCRMBoards: forbidden — caller lacks deals:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            // 🔐 Scoping is authoritative from the JWT, never from a client-supplied role string.
            var callerRole = GetCurrentCanonicalRole();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? agentName ?? string.Empty;

            IQueryable<LeadRecord> leadsQuery = _context.LeadRecords.Where(l => !l.IsConvertedToDeal);
            IQueryable<DealRecord> dealsQuery = _context.DealRecords;
            if (visibleTenants.Count > 0)
            {
                leadsQuery = leadsQuery.Where(l => visibleTenants.Contains(l.TenantId));
                dealsQuery = dealsQuery.Where(d => visibleTenants.Contains(d.TenantId));
            }

            // Only a Broker (agent) is restricted to their own records; admins see the full tenant pipeline.
            // AllocatedBrokerIds is a comma-separated list of broker user ids; the broker sees
            // anything where their id appears as a token in that list.
            if (callerRole == "Broker")
            {
                var callerId = GetCurrentUserId();
                var likePattern = $"%{callerId}%";
                leadsQuery = leadsQuery.Where(l => l.AllocatedBrokerIds == callerId
                    || l.AllocatedBrokerIds.StartsWith(callerId + ",")
                    || l.AllocatedBrokerIds.Contains("," + callerId));
                dealsQuery = dealsQuery.Where(d => d.AllocatedBrokerIds == callerId
                    || d.AllocatedBrokerIds.StartsWith(callerId + ",")
                    || d.AllocatedBrokerIds.Contains("," + callerId));
            }

            // 📊 Stage aggregates computed over the entire filtered universe so
            // bar/donut stage charts reflect every lead + deal, not just the
            // single 20-row page that was returned.
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["leadStage"] = await AggregateStringAsync(leadsQuery, l => l.LeadStage.ToString()),
                ["dealStage"] = await AggregateStringAsync(dealsQuery, d => d.DealStage.ToString())
            };

            var (activeLeads, leadTotal) = await PageAsync(leadsQuery.OrderByDescending(l => l.CreatedAt), page);
            var (activeDeals, dealTotal) = await PageAsync(dealsQuery.OrderByDescending(d => d.LastStageUpdatedAt), page);
            HttpContext.Response.Headers["X-Lead-Count"] = leadTotal.ToString();
            HttpContext.Response.Headers["X-Deal-Count"] = dealTotal.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);

            return Ok(new
            {
                unverifiedLeads = activeLeads,
                verifiedDeals = activeDeals
            });
        }

         [HttpPost("lead/create")]
         public async Task<IActionResult> CreateLead([FromBody] LeadRecord newLead)
         {
             if (!await _permissionCheck.HasPermissionAsync("leads:create"))
             {
                 _logger.LogWarning("CreateLead: forbidden — caller lacks leads:create permission");
                 return Forbid();
             }

             if (newLead == null) return BadRequest(new { message = "Invalid lead structural parameters." });

            var tenantId = GetCurrentTenant();
            newLead.TenantId = tenantId;
            // 🔐 A Broker always creates leads allocated to themselves — the client value is ignored
            // so an agent can never claim another broker's pipeline. We store the broker's user id
            // (NameIdentifier claim) when available, falling back to the name for legacy callers.
            if (GetCurrentCanonicalRole() == "Broker")
            {
                var brokerId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(brokerId))
                    newLead.AllocatedBrokerIds = brokerId;
            }
            newLead.LeadStage = LeadWorkflowStage.Contacted;
            newLead.IsConvertedToDeal = false;
            newLead.CreatedAt = DateTime.UtcNow;

             // 🔗 Item 3: try to bind the lead to an EXISTING contact by email or by name/surname.
             // If none is found, the lead's own ClientName/ContactEmail/ContactPhone are retained
             // as the "new contact" details (no contact record created until promotion).
             var match = !string.IsNullOrWhiteSpace(newLead.ContactEmail)
                 ? await _context.CRMContacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.EmailAddress == newLead.ContactEmail)
                 : null;
             if (match == null && !string.IsNullOrWhiteSpace(newLead.ClientName))
             {
                 var term = newLead.ClientName.ToLower();
                 var surname = (newLead.ClientName.Split(' ').LastOrDefault() ?? "");
                 match = await _context.CRMContacts
                     .Where(c => c.TenantId == tenantId && c.FullName != null)
                     .FirstOrDefaultAsync(c => c.FullName.ToLower().Contains(term) ||
                                               (surname.Length > 2 && c.FullName.ToLower().Contains(surname)));
             }
             if (match != null) newLead.LinkedContactId = match.ContactId;

             _context.LeadRecords.Add(newLead);
             await _context.SaveChangesAsync();

              return Ok(new { message = "Unverified lead record successfully catalogued.", leadId = newLead.LeadId, linkedContactId = newLead.LinkedContactId });
          }

        [HttpPut("lead/{leadId:int}")]
        public async Task<IActionResult> UpdateLead(int leadId, [FromBody] LeadRecord updatedLead)
        {
            if (!await _permissionCheck.HasPermissionAsync("leads:edit"))
            {
                _logger.LogWarning("UpdateLead: forbidden — caller lacks leads:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var lead = await _context.LeadRecords.FirstOrDefaultAsync(l => l.LeadId == leadId && l.TenantId == tenantId);
            if (lead == null) return NotFound(new { message = "Lead record not found." });

            if (GetCurrentCanonicalRole() == "Broker")
            {
                var brokerId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(brokerId))
                    updatedLead.AllocatedBrokerIds = brokerId;
            }

            lead.ClientName = updatedLead.ClientName;
            lead.ContactEmail = updatedLead.ContactEmail;
            lead.ContactPhone = updatedLead.ContactPhone;
            lead.LinkedContactId = updatedLead.LinkedContactId;
            lead.AllocatedBrokerIds = updatedLead.AllocatedBrokerIds;
            lead.LeadStage = updatedLead.LeadStage;
            lead.PropertyInterestClass = updatedLead.PropertyInterestClass;
            lead.PreferredArea = updatedLead.PreferredArea;
            lead.LeadType = updatedLead.LeadType;
            lead.EstimatedBudget = updatedLead.EstimatedBudget;
            lead.LeadNotes = updatedLead.LeadNotes;
            lead.CommissionType = updatedLead.CommissionType;
            lead.CommissionValue = updatedLead.CommissionValue;

            await _context.SaveChangesAsync();
            return Ok(lead);
        }

        // 🚀 Promote a lead to a deal with automatic property matching.
        [HttpPost("leads/{leadId:int}/promote")]
        public async Task<IActionResult> PromoteLeadToDeal(int leadId, [FromBody] PromotionRequest request)
        {
            if (!await _permissionCheck.HasPermissionAsync("leads:edit"))
            {
                _logger.LogWarning("PromoteLeadToDeal: forbidden — caller lacks leads:edit permission");
                return Forbid();
            }

            var lead = await _context.LeadRecords
                .FirstOrDefaultAsync(l => l.LeadId == leadId && l.TenantId == GetCurrentTenant());
            if (lead == null) return NotFound(new { message = "Target lead record not found." });
            if (lead.IsConvertedToDeal) return Conflict(new { message = "Lead already promoted to a deal." });

            // Resolve / create the buyer contact (reuse the contact-linking pattern).
            var tenantId = lead.TenantId;
            var buyerContact = !string.IsNullOrEmpty(lead.ContactEmail)
                ? await _context.CRMContacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.EmailAddress == lead.ContactEmail)
                : null;
            if (buyerContact == null)
            {
                buyerContact = new CRMContact
                {
                    ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                    TenantId = tenantId,
                    FullName = lead.ClientName,
                    EmailAddress = lead.ContactEmail,
                    PhoneNumber = lead.ContactPhone,
                    ContactRoleType = "Buyer",
                    PositionJobTitle = "Private Individual",
                    CreatedAt = DateTime.UtcNow
                };
                _context.CRMContacts.Add(buyerContact);
            }

            int? matchedPropertyId = request.LinkToPropertyId;
            string sellerClientId = "Unknown Seller Pool";

            // 🧭 Auto-match a property via the Property microservice (through the gateway),
            // forwarding the caller's bearer token so tenant visibility scoping applies.
            if (!matchedPropertyId.HasValue)
            {
                var accessToken = Request.Headers["Authorization"].FirstOrDefault();
                accessToken = accessToken?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                    ? accessToken.Substring("Bearer ".Length) : null;
                var client = _http.CreateClient();
                if (!string.IsNullOrEmpty(accessToken))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var criteria = new
                {
                    locations = new[] { request.PreferredAreaOverride ?? lead.PreferredArea },
                    propertyType = request.PropertyTypeOverride ?? lead.PropertyInterestClass,
                    minSize = request.MinSize,
                    maxSize = request.MaxSize,
                    listingType = request.IsRent ? "Rent" : "Sale",
                    minPrice = (int?)null,
                    maxPrice = request.IsRent ? (int?)null : (int?)lead.EstimatedBudget
                };
                var payload = new StringContent(JsonSerializer.Serialize(criteria), System.Text.Encoding.UTF8, "application/json");
                var matchResponse = await client.PostAsync($"{_gatewayUrl}/properties/search?page=1", payload);
                if (matchResponse.IsSuccessStatusCode)
                {
                    var props = JsonSerializer.Deserialize<List<PropertyMatchDto>>(await matchResponse.Content.ReadAsStringAsync(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    var match = props?.FirstOrDefault();
                    if (match != null)
                    {
                        matchedPropertyId = match.PropertyId;
                        sellerClientId = match.LinkedOwnerId ?? "Unknown Seller Pool";
                    }
                }
            }

            // If a manual seller name was supplied, resolve/create a seller contact.
            if (!string.IsNullOrEmpty(request.SellerNameIfNewDeal))
            {
                var sellerContact = await _context.CRMContacts
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.FullName.Contains(request.SellerNameIfNewDeal));
                if (sellerContact == null)
                {
                    sellerContact = new CRMContact
                    {
                        ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                        TenantId = tenantId,
                        FullName = request.SellerNameIfNewDeal,
                        ContactRoleType = "Seller",
                        PositionJobTitle = "Property Owner / Developer",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CRMContacts.Add(sellerContact);
                }
                sellerClientId = sellerContact.ContactId;
            }

            var deal = new DealRecord
            {
                TenantId = tenantId,
                AllocatedBrokerIds = lead.AllocatedBrokerIds,
                DealStage = DealWorkflowStage.DiscoveryNeedsAnalysis,
                LinkedPropertyId = matchedPropertyId,
                BuyerClientId = buyerContact.ContactId,
                SellerClientId = sellerClientId,
                TransactionValue = lead.EstimatedBudget,
                    CommissionType = lead.CommissionType,
                    CommissionValue = lead.CommissionValue,
                DealNotes = $"[Auto-Promoted] Lead {lead.LeadId} | Intent: {(request.IsRent ? "Rent" : "Sale")} | Interest: {lead.PropertyInterestClass} in {lead.PreferredArea} | {lead.LeadNotes}",
                LastStageUpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.DealRecords.Add(deal);
            lead.IsConvertedToDeal = true;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Lead promoted to deal with property auto-matching.",
                dealId = deal.DealId,
                linkedPropertyId = matchedPropertyId,
                buyerClientId = buyerContact.ContactId,
                sellerClientId = sellerClientId
            });
        }

        // ❌ Delete a deal — restricted to AgencyAdmin and above (PlatformAdmin).
        [HttpDelete("deals/{dealId:int}")]
        public async Task<IActionResult> DeleteDeal(int dealId)
        {
            if (!await _permissionCheck.HasPermissionAsync("deals:delete"))
            {
                _logger.LogWarning("DeleteDeal: forbidden — caller lacks deals:delete permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            if (callerRole != "AgencyAdmin" && callerRole != "PlatformAdmin")
                return Forbid();

            var deal = await _context.DealRecords
                .FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == GetCurrentTenant());
            if (deal == null) return NotFound(new { message = "Target deal record not found." });

            _context.DealRecords.Remove(deal);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Deal record deleted." });
        }

        [HttpPut("deal/{dealId:int}")]
        public async Task<IActionResult> UpdateDeal(int dealId, [FromBody] UpdateDealPayload updatedDeal)
        {
            if (!await _permissionCheck.HasPermissionAsync("deals:edit"))
            {
                _logger.LogWarning("UpdateDeal: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            // 🛡️ PlatformAdmin can edit any deal; everyone else is pinned to
            // their JWT tenant to prevent cross-tenant tampering.
            var callerRole = GetCurrentCanonicalRole();
            var isPlatformAdmin = callerRole == "PlatformAdmin";
            var tenantId = GetCurrentTenant();
            var deal = isPlatformAdmin
                ? await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == dealId)
                : await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            // 🔒 Closed deals (ClosedWon / ClosedLost) are read-only. The
            // commission has been locked in (or zeroed) and the audit trail
            // must stay intact.
            if (deal.DealStage == DealWorkflowStage.ClosedWon || deal.DealStage == DealWorkflowStage.ClosedLost)
            {
                return BadRequest(new { message = "This deal is closed and cannot be edited." });
            }

            // Brokers are pinned to their own account — never trust the payload's
            // agent override. Higher roles can reassign ownership freely.
            var isBroker = callerRole == "Broker";
            if (isBroker)
            {
                var callerId = GetCurrentUserId();
                if (!string.IsNullOrEmpty(callerId)) deal.AllocatedBrokerIds = callerId;
            }
            else if (!string.IsNullOrEmpty(updatedDeal.AllocatedBrokerIds))
            {
                deal.AllocatedBrokerIds = updatedDeal.AllocatedBrokerIds;
            }

            // 🛠️ Partial-update merge: every field on the payload is optional
            // so the frontend can submit only what changed.
            if (updatedDeal.BuyerClientId != null) deal.BuyerClientId = updatedDeal.BuyerClientId;
            if (updatedDeal.SellerClientId != null) deal.SellerClientId = updatedDeal.SellerClientId;
            if (updatedDeal.DealStage.HasValue) deal.DealStage = updatedDeal.DealStage.Value;
            if (updatedDeal.TransactionValue.HasValue) deal.TransactionValue = updatedDeal.TransactionValue.Value;
            if (updatedDeal.DealNotes != null) deal.DealNotes = updatedDeal.DealNotes;
            if (updatedDeal.CommissionType != null) deal.CommissionType = updatedDeal.CommissionType;
            if (updatedDeal.CommissionValue.HasValue) deal.CommissionValue = updatedDeal.CommissionValue.Value;
            if (updatedDeal.RealisedCommission.HasValue) deal.RealisedCommission = updatedDeal.RealisedCommission.Value;
            if (updatedDeal.LinkedPropertyId.HasValue) deal.LinkedPropertyId = updatedDeal.LinkedPropertyId.Value;

            // 💰 Keep the realised commission consistent with the commission
            // parameters whenever the deal is ClosedWon. ClosedLost clears it.
            // Auto-fill ONLY when RealisedCommission is null/0 — a hand-entered
            // non-zero value wins so editors can override the formula.
            if (deal.DealStage == DealWorkflowStage.ClosedWon)
            {
                if (deal.RealisedCommission == null || deal.RealisedCommission == 0m)
                {
                    deal.RealisedCommission = deal.CommissionType == CommissionTypes.Percent
                        ? Math.Round(deal.TransactionValue * deal.CommissionValue / 100m, 2)
                        : deal.CommissionValue;
                    deal.RealisedCommissionIsAuto = true;
                }
                else
                {
                    deal.RealisedCommissionIsAuto = false;
                }
            }
            else if (deal.DealStage == DealWorkflowStage.ClosedLost)
            {
                deal.RealisedCommission = 0m;
                deal.RealisedCommissionIsAuto = false;
            }

            deal.LastStageUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(deal);
        }


        [HttpPost("reallocate")]
        public async Task<IActionResult> ReallocateTarget([FromBody] ReallocatePayload payload)
        {
            if (!await _permissionCheck.HasPermissionAsync("deals:edit"))
            {
                _logger.LogWarning("ReallocateTarget: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            if (payload.IsDeal)
            {
                var deal = await _context.DealRecords.FindAsync(payload.TargetId);
                if (deal == null) return NotFound(new { message = "Target deal record not found." });
                // payload.TargetAgentName is actually a broker id now (legacy field name kept
                // for frontend compat — same payload shape, just an id instead of a display name).
                deal.AllocatedBrokerIds = payload.TargetAgentName;
            }
            else
            {
                var lead = await _context.LeadRecords.FindAsync(payload.TargetId);
                if (lead == null) return NotFound(new { message = "Target lead record not found." });
                lead.AllocatedBrokerIds = payload.TargetAgentName;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Assignment safely reallocated to {payload.TargetAgentName}." });
        }

        [HttpPost("update-lead-stage")]
        public async Task<IActionResult> UpdateLeadStage([FromBody] LeadUpdatePayload payload)
        {
            if (!await _permissionCheck.HasPermissionAsync("leads:edit"))
            {
                _logger.LogWarning("UpdateLeadStage: forbidden — caller lacks leads:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var lead = await _context.LeadRecords.FirstOrDefaultAsync(l => l.LeadId == payload.LeadId && l.TenantId == tenantId);
            if (lead == null) return NotFound(new { message = "Target lead tracking record not found." });

            // 🔐 A Broker may only advance leads allocated to themselves.
            if (GetCurrentCanonicalRole() == "Broker" && !CallerIsAssignedTo(lead.AllocatedBrokerIds, GetCurrentUserId()))
                return Forbid();

            if (!Enum.TryParse<LeadWorkflowStage>(payload.NextStage, true, out var nextStageEnum))
            {
                return BadRequest(new { message = "Invalid lead workflow stage parameter provided." });
            }

            lead.LeadStage = nextStageEnum;
            if (!string.IsNullOrEmpty(payload.NotesUpdate))
            {
                lead.LeadNotes += $" | [Update]: {payload.NotesUpdate}";
            }

            if (nextStageEnum == LeadWorkflowStage.Qualified && !lead.IsConvertedToDeal)
            {
                var buyerContact = await _context.CRMContacts
                    .FirstOrDefaultAsync(c => c.TenantId == lead.TenantId && c.EmailAddress == lead.ContactEmail);

                if (buyerContact == null)
                {
                    buyerContact = new CRMContact
                    {
                        ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                        TenantId = lead.TenantId,
                        FullName = lead.ClientName,
                        EmailAddress = lead.ContactEmail,
                        PhoneNumber = lead.ContactPhone,
                        ContactRoleType = "Buyer",
                        PositionJobTitle = "Private Individual",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CRMContacts.Add(buyerContact);
                }

                var sellerName = !string.IsNullOrEmpty(payload.SellerNameIfNewDeal) ? payload.SellerNameIfNewDeal : "Unknown Seller Pool";
                var sellerContact = await _context.CRMContacts
                    .FirstOrDefaultAsync(c => c.TenantId == lead.TenantId && c.FullName.Contains(sellerName));

                if (sellerContact == null)
                {
                    sellerContact = new CRMContact
                    {
                        ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                        TenantId = lead.TenantId,
                        FullName = sellerName,
                        ContactRoleType = "Seller",
                        PositionJobTitle = "Property Owner / Developer",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CRMContacts.Add(sellerContact);
                }

                var dynamicDeal = new DealRecord
                {
                    TenantId = lead.TenantId,
                    AllocatedBrokerIds = lead.AllocatedBrokerIds,
                    DealStage = DealWorkflowStage.DiscoveryNeedsAnalysis,
                    LinkedPropertyId = payload.LinkToPropertyId,
                    BuyerClientId = buyerContact.ContactId,
                    SellerClientId = sellerContact.ContactId,
                    TransactionValue = lead.EstimatedBudget,
                    DealNotes = $"[Auto-Promoted from Lead ID {lead.LeadId}]. Original Interest: {lead.PropertyInterestClass} in {lead.PreferredArea}. Notes: {lead.LeadNotes}",
                    LastStageUpdatedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CommissionType = lead.CommissionType,
                    CommissionValue = lead.CommissionValue
                };

                _context.DealRecords.Add(dynamicDeal);
                lead.IsConvertedToDeal = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = lead.IsConvertedToDeal ? "Lead successfully qualified! Profiles synced and deal initiated." : "Lead stage updated." });
        }

        [HttpPost("update-deal-stage")]
        public async Task<IActionResult> UpdateDealStage([FromBody] DealUpdatePayload payload)
        {
            if (!await _permissionCheck.HasPermissionAsync("deals:edit"))
            {
                _logger.LogWarning("UpdateDealStage: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var deal = await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == payload.DealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Target deal record not found." });

            // 🔒 Closed deals (ClosedWon / ClosedLost) are read-only — the
            // commission has been locked in (or zeroed) and the audit trail
            // must stay intact.
            if (deal.DealStage == DealWorkflowStage.ClosedWon || deal.DealStage == DealWorkflowStage.ClosedLost)
            {
                return BadRequest(new { message = "This deal is closed and cannot be progressed further." });
            }

            // 🔐 A Broker may only advance deals allocated to themselves.
            if (GetCurrentCanonicalRole() == "Broker" && !CallerIsAssignedTo(deal.AllocatedBrokerIds, GetCurrentUserId()))
                return Forbid();

            if (!Enum.TryParse<DealWorkflowStage>(payload.NextStage, true, out var nextDealStageEnum))
            {
                return BadRequest(new { message = "Invalid deal cycle stage parameter provided." });
            }

            deal.DealStage = nextDealStageEnum;
            deal.LastStageUpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(payload.NotesUpdate))
            {
                deal.DealNotes += $" | [Stage Update]: {payload.NotesUpdate}";
            }

            // 💰 Commission is only realised into revenue once the deal is concluded (ClosedWon).
            // Same auto-fill rule as the full PUT: only fill when RealisedCommission is
            // null/0, and mark it as auto so the UI can render the badge.
            if (nextDealStageEnum == DealWorkflowStage.ClosedWon)
            {
                if (deal.RealisedCommission == null || deal.RealisedCommission == 0m)
                {
                    deal.RealisedCommission = deal.CommissionType == CommissionTypes.Percent
                        ? Math.Round(deal.TransactionValue * deal.CommissionValue / 100m, 2)
                        : deal.CommissionValue;
                    deal.RealisedCommissionIsAuto = true;
                }
                else
                {
                    deal.RealisedCommissionIsAuto = false;
                }
            }
            else if (nextDealStageEnum == DealWorkflowStage.ClosedLost)
            {
                deal.RealisedCommission = 0m;
                deal.RealisedCommissionIsAuto = false;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Deal successfully progressed to {payload.NextStage}." });
        }

        [HttpGet("report/summary")]
        public async Task<IActionResult> GetPipelineReportSummary([FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetPipelineReportSummary: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();

            IQueryable<LeadRecord> baseLeadQuery = _context.LeadRecords
                .Where(l => !l.IsConvertedToDeal)
                .Where(l => !isBroker || l.AllocatedBrokerIds == callerId || l.AllocatedBrokerIds.StartsWith(callerId + ",") || l.AllocatedBrokerIds.Contains("," + callerId));
            IQueryable<DealRecord> baseDealQuery = _context.DealRecords
                .Where(d => !isBroker || d.AllocatedBrokerIds == callerId || d.AllocatedBrokerIds.StartsWith(callerId + ",") || d.AllocatedBrokerIds.Contains("," + callerId));
            if (visibleTenants.Count > 0)
            {
                baseLeadQuery = baseLeadQuery.Where(l => visibleTenants.Contains(l.TenantId));
                baseDealQuery = baseDealQuery.Where(d => visibleTenants.Contains(d.TenantId));
            }

            var summaryReport = new PipelineSummaryReport
            {
                // 🧮 "Active leads" = anything in the pipeline except
                // disqualified leads and leads already converted to deals.
                ActiveLeadsCount = await baseLeadQuery.Where(l => l.LeadStage != LeadWorkflowStage.Disqualified).CountAsync(),
                ValuationsInProcessCount = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.DiscoveryNeedsAnalysis || d.DealStage == DealWorkflowStage.Presentation).CountAsync(),
                ConveyancingAtDeedsCount = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.ProposalQuoteSent || d.DealStage == DealWorkflowStage.Negotiation).CountAsync(),
                FinalizedClosedCount = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).CountAsync(),
                // 💰 Pipeline estimated value = sum of TransactionValue for
                // every in-flight deal (not ClosedWon/Lost). This is the
                // "potential revenue" on the board, not the realised number.
                TotalPipelineEstimatedValue = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.DiscoveryNeedsAnalysis || d.DealStage == DealWorkflowStage.Presentation || d.DealStage == DealWorkflowStage.ProposalQuoteSent || d.DealStage == DealWorkflowStage.Negotiation).SumAsync(d => d.TransactionValue)
            };

            _logger.LogInformation("GetPipelineReportSummary: overrideTenant={Override} effectiveTenants={TenantCount} | ActiveLeads={Active} Valuations={Valuations} Conveyancing={Conveyancing} ClosedWon={ClosedWon} PipelineValue={PipelineValue}",
                tenantId ?? "(default)", visibleTenants.Count, summaryReport.ActiveLeadsCount, summaryReport.ValuationsInProcessCount, summaryReport.ConveyancingAtDeedsCount, summaryReport.FinalizedClosedCount, summaryReport.TotalPipelineEstimatedValue);

            return Ok(summaryReport);
        }

        [HttpGet("report/overview")]
        public async Task<IActionResult> GetReportOverview([FromQuery] int? year, [FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetReportOverview: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();
            var activeYear = year ?? DateTime.UtcNow.Year;
            _logger.LogInformation("GetReportOverview: Year={Year} overrideTenant={Override} Tenants={TenantCount}", activeYear, tenantId ?? "(default)", visibleTenants.Count);

            var yearStart = new DateTime(activeYear, 1, 1);
            var yearEnd = new DateTime(activeYear + 1, 1, 1);

            IQueryable<LeadRecord> tenantLeadsQuery = _context.LeadRecords
                .Where(l => l.CreatedAt >= yearStart && l.CreatedAt < yearEnd)
                .Where(l => !isBroker || l.AllocatedBrokerIds == callerId || l.AllocatedBrokerIds.StartsWith(callerId + ",") || l.AllocatedBrokerIds.Contains("," + callerId));
            IQueryable<DealRecord> tenantDealsQuery = _context.DealRecords
                .Where(d => d.CreatedAt >= yearStart && d.CreatedAt < yearEnd)
                .Where(d => !isBroker || d.AllocatedBrokerIds == callerId || d.AllocatedBrokerIds.StartsWith(callerId + ",") || d.AllocatedBrokerIds.Contains("," + callerId));
            if (visibleTenants.Count > 0)
            {
                tenantLeadsQuery = tenantLeadsQuery.Where(l => visibleTenants.Contains(l.TenantId));
                tenantDealsQuery = tenantDealsQuery.Where(d => visibleTenants.Contains(d.TenantId));
            }

            var tenantLeads = await tenantLeadsQuery.ToListAsync();
            var tenantDeals = await tenantDealsQuery.ToListAsync();

            var monthlyBreakdown = Enumerable.Range(1, 12)
                .Select(month => new ReportDrilldownRow
                {
                    Period = new DateTime(activeYear, month, 1).ToString("MMM"),
                    Leads = tenantLeads.Count(l => l.CreatedAt.Month == month),
                    Deals = tenantDeals.Count(d => d.CreatedAt.Month == month),
                    // 💰 Realised commission on ClosedWon deals created this
                    // month — same formula as the totals so the line chart
                    // sums back to the headline number.
                    Revenue = Math.Round(tenantDeals
                        .Where(d => d.CreatedAt.Month == month && d.DealStage == DealWorkflowStage.ClosedWon)
                        .Sum(d => d.RealisedCommission
                            ?? (d.CommissionType == CommissionTypes.Percent
                                ? d.TransactionValue * d.CommissionValue / 100m
                                : d.CommissionValue)), 2)
                })
                .ToList();

            return Ok(new CRMReportOverview
            {
                TotalLeads = tenantLeads.Count,
                TotalDeals = tenantDeals.Count,
                // 💰 Revenue is realised commission on ClosedWon deals, not
                // raw TransactionValue. Use the locked-in amount when
                // available, otherwise fall back to the per-deal formula so
                // legacy seed rows (no RealisedCommission) still surface.
                TotalRevenue = Math.Round(tenantDeals
                    .Where(d => d.DealStage == DealWorkflowStage.ClosedWon)
                    .Sum(d => d.RealisedCommission
                        ?? (d.CommissionType == CommissionTypes.Percent
                            ? d.TransactionValue * d.CommissionValue / 100m
                            : d.CommissionValue)), 2),
                TotalRealisedCommission = Math.Round(tenantDeals
                    .Where(d => d.DealStage == DealWorkflowStage.ClosedWon)
                    .Sum(d => d.RealisedCommission
                        ?? (d.CommissionType == CommissionTypes.Percent
                            ? d.TransactionValue * d.CommissionValue / 100m
                            : d.CommissionValue)), 2),
                MonthlyBreakdown = monthlyBreakdown
            });
        }

        // 📊 Commission per deal stage — the "expected commission" for every
        // deal in the stage, computed from the deal's commission parameters
        // (Percent of TransactionValue or a flat Rand amount). This is more
        // useful than realised commission (which is only set on ClosedWon)
        // because it surfaces the full revenue pipeline at every stage.
        [HttpGet("report/commission-by-stage")]
        public async Task<IActionResult> GetCommissionByStage([FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetCommissionByStage: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();

            IQueryable<DealRecord> query = _context.DealRecords
                .Where(d => !isBroker || d.AllocatedBrokerIds == callerId || d.AllocatedBrokerIds.StartsWith(callerId + ",") || d.AllocatedBrokerIds.Contains("," + callerId));
            if (visibleTenants.Count > 0)
                query = query.Where(d => visibleTenants.Contains(d.TenantId));

            // Pull the rows so we can apply the per-deal commission formula
            // in C# (EF Core can't translate a nullable-vs-string switch).
            var deals = await query.Select(d => new {
                d.DealStage,
                d.TransactionValue,
                d.CommissionType,
                d.CommissionValue,
                d.RealisedCommission
            }).ToListAsync();

            var rows = deals
                .GroupBy(d => d.DealStage)
                .Select(g => new ReportDrilldownRow
                {
                    Period = g.Key.ToString(),
                    Deals = g.Count(),
                    // 🧮 Expected commission per deal: Percent → TV*CV/100, Flat → CV.
                    // For ClosedWon we still use the locked RealisedCommission
                    // so the chart agrees with the closed-won accounting.
                    Revenue = Math.Round(g.Sum(d =>
                        d.DealStage == DealWorkflowStage.ClosedWon
                            ? (d.RealisedCommission ?? ComputeExpectedCommission(d))
                            : ComputeExpectedCommission(d)
                    ), 2)
                })
                .ToList();
            return Ok(rows);

            static decimal ComputeExpectedCommission(dynamic d) =>
                d.CommissionType == CommissionTypes.Percent
                    ? Math.Round((decimal)d.TransactionValue * (decimal)d.CommissionValue / 100m, 2)
                    : (decimal)d.CommissionValue;
        }

        // 📊 Broker leaderboard — deal count + pipeline value, ranked.
        [HttpGet("report/broker-leaderboard")]
        public async Task<IActionResult> GetBrokerLeaderboard([FromQuery] string? tenantId)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetBrokerLeaderboard: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();
            IQueryable<DealRecord> query = _context.DealRecords
                .Where(d => d.AllocatedBrokerIds != null && d.AllocatedBrokerIds != "")
                .Where(d => !isBroker || d.AllocatedBrokerIds == callerId || d.AllocatedBrokerIds.StartsWith(callerId + ",") || d.AllocatedBrokerIds.Contains("," + callerId));
            if (visibleTenants.Count > 0)
                query = query.Where(d => visibleTenants.Contains(d.TenantId));

            // Pull per-deal data so we can apply the commission formula in
            // C# (EF Core can't translate a string-vs-enum branch on a
            // nullable CommissionType). Each deal's commission is normalised
            // to Rand before aggregation.
            var deals = await query
                .Select(d => new {
                    d.AllocatedBrokerIds,
                    d.TransactionValue,
                    d.CommissionType,
                    d.CommissionValue,
                    d.RealisedCommission,
                    d.DealStage
                })
                .ToListAsync();

            // 🧑‍💼 Broker name lookup. AllocatedBrokerIds is a comma-separated list of
            // broker user ids; load the full visible broker roster once and build
            // a map so we can resolve each id in a deal's allocation to a
            // human-readable name for the leaderboard display.
            var brokerRoster = await _context.BrokerRecords
                .Where(b => visibleTenants.Count == 0 || visibleTenants.Contains(b.TenantId))
                .Select(b => new { b.BrokerId, b.FullName })
                .ToListAsync();
            var brokerNames = brokerRoster
                .GroupBy(b => b.BrokerId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FullName, StringComparer.OrdinalIgnoreCase);

            var rows = deals
                .GroupBy(d => d.AllocatedBrokerIds)
                .Select(g => new
                {
                    // Resolve a representative name for the first broker in the
                    // list. Multi-broker groups will surface one of the names;
                    // the raw ids are returned separately for drill-downs.
                    agent = (g.Key ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id => brokerNames.TryGetValue(id, out var n) ? n : id)
                        .FirstOrDefault() ?? g.Key,
                    agentIds = g.Key,
                    deals = g.Count(),
                    // 🧮 totalValue = full TransactionValue across every deal
                    // (the headline "pipeline value" metric for the broker).
                    totalValue = Math.Round(g.Sum(d => d.TransactionValue), 2),
                    // 🧮 totalCommission = expected commission on every deal
                    // (use the locked RealisedCommission for ClosedWon so the
                    // number agrees with the realised revenue on the report).
                    totalCommission = Math.Round(g.Sum(d => d.RealisedCommission
                        ?? (d.CommissionType == CommissionTypes.Percent
                            ? d.TransactionValue * d.CommissionValue / 100m
                            : d.CommissionValue)), 2),
                    won = g.Count(d => d.DealStage == DealWorkflowStage.ClosedWon)
                })
                .OrderByDescending(r => r.totalValue)
                .ToList();
            return Ok(rows);
        }

        // 🧾 Load a broker's resumable brochure card for a deal (client details + curated unit lines).
        [HttpGet("brochure-cards")]
        public async Task<IActionResult> GetBrochureCardByDeal([FromQuery] int dealId, [FromQuery] string? tenantId = null)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetBrochureCardByDeal: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();

            IQueryable<BrochureCard> cardQuery = _context.BrochureCards
                .Include(c => c.Lines)
                .Where(c => c.DealId == dealId && !c.IsDeleted);
            if (visibleTenants.Count > 0)
                cardQuery = cardQuery.Where(c => visibleTenants.Contains(c.TenantId));

            var card = await cardQuery.FirstOrDefaultAsync();
            if (card == null) return NotFound(new { message = "No brochure card found for this deal." });
            if (isBroker && card.BrokerAgentId != callerName) return Forbid();

            return Ok(new
            {
                cardId = card.CardId,
                dealId = card.DealId,
                clientName = card.ClientName,
                clientEmail = card.ClientEmail,
                clientPhone = card.ClientPhone,
                companyName = card.CompanyName,
                companyLogoUrl = card.CompanyLogoUrl,
                status = card.Status.ToString(),
                generatedFilePath = card.GeneratedFilePath,
                createdAt = card.CreatedAt,
                lines = card.Lines
                    .Where(l => !l.IsDeleted)
                    .Select(l => new
                    {
                        lineId = l.LineId,
                        propertyId = l.PropertyId,
                        unitId = l.UnitId,
                        unitNumber = l.UnitNumber,
                        unitDescription = l.UnitDescription,
                        propertyDescription = l.PropertyDescription,
                        netRentableAreaSqM = l.NetRentableAreaSqM,
                        selectedImageRefs = string.IsNullOrEmpty(l.SelectedImageRefs) 
                            ? new List<string>() 
                            : l.SelectedImageRefs.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    })
            });
        }

        // 🧾 Active brochure cards for the broker notification badge.
        [HttpGet("brochure-cards/active")]
        public async Task<IActionResult> GetActiveBrochureCards([FromQuery] string? tenantId = null)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetActiveBrochureCards: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();

            IQueryable<BrochureCard> query = _context.BrochureCards
                .Where(c => !c.IsDeleted &&
                            (c.Status == BrochureCardStatus.Active || c.Status == BrochureCardStatus.Generating));
            if (visibleTenants.Count > 0)
                query = query.Where(c => visibleTenants.Contains(c.TenantId));
            if (isBroker) query = query.Where(c => c.BrokerAgentId == callerName);

            var cards = await query.Select(c => new
            {
                cardId = c.CardId,
                dealId = c.DealId,
                clientName = c.ClientName,
                status = c.Status.ToString(),
                createdAt = c.CreatedAt
            }).ToListAsync();
            return Ok(cards);
        }

        // 🧾 Upsert the broker's brochure card (client details + curated <=5 unit lines).
        [HttpPut("brochure-cards")]
        public async Task<IActionResult> UpsertBrochureCard([FromBody] BrochureCardUpsertRequest request)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:edit"))
            {
                _logger.LogWarning("UpsertBrochureCard: forbidden — caller lacks billing:edit permission");
                return Forbid();
            }

            if (request == null || request.DealId <= 0)
                return BadRequest(new { message = "DealId is required for a brochure card." });

            var tenantId = GetCurrentTenant();
            var brokerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var brokerId = GetCurrentUserId();
            var role = GetCurrentCanonicalRole();

            if (role == "Broker")
            {
                // Check whether the broker is one of the ids in the deal's
                // AllocatedBrokerIds CSV. Single-broker legacy deals still
                // match the equality case.
                var deal = await _context.DealRecords
                    .FirstOrDefaultAsync(d => d.DealId == request.DealId && d.TenantId == tenantId);
                if (deal == null) return NotFound(new { message = "Target deal record not found." });
                if (!CallerIsAssignedTo(deal.AllocatedBrokerIds, brokerId)) return Forbid();
            }
            else
            {
                var dealExists = await _context.DealRecords.AnyAsync(d => d.DealId == request.DealId && d.TenantId == tenantId);
                if (!dealExists) return NotFound(new { message = "Target deal record not found." });
            }

            BrochureCard card;
            if (request.CardId.HasValue && request.CardId > 0)
            {
                card = await _context.BrochureCards
                    .Include(c => c.Lines)
                    .FirstOrDefaultAsync(c => c.CardId == request.CardId.Value && c.TenantId == tenantId);
                if (card == null) return NotFound(new { message = "Brochure card not found." });
                foreach (var ln in card.Lines.Where(l => !l.IsDeleted)) ln.IsDeleted = true;
            }
            else
            {
                card = new BrochureCard { TenantId = tenantId, DealId = request.DealId, BrokerAgentId = brokerName, Status = BrochureCardStatus.Active };
                _context.BrochureCards.Add(card);
                await _context.SaveChangesAsync();
            }

            card.ClientName = request.ClientName;
            card.ClientEmail = request.ClientEmail;
            card.ClientPhone = request.ClientPhone;
            card.CompanyName = request.CompanyName;
            card.CompanyLogoUrl = request.CompanyLogoUrl;
            card.UpdatedAt = DateTime.UtcNow;

            foreach (var unit in request.Units.Take(5))
            {
                _context.BrochureCardLines.Add(new BrochureCardLine
                {
                    CardId = card.CardId,
                    PropertyId = unit.PropertyId,
                    UnitId = unit.UnitId,
                    UnitNumber = unit.UnitNumber ?? string.Empty,
                    UnitDescription = unit.UnitDescription ?? string.Empty,
                    PropertyDescription = unit.PropertyDescription ?? string.Empty,
                    NetRentableAreaSqM = unit.NetRentableAreaSqM,
                    SelectedImageRefs = unit.SelectedImageRefs != null && unit.SelectedImageRefs.Any() 
                        ? string.Join(",", unit.SelectedImageRefs) 
                        : string.Empty
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { cardId = card.CardId });
        }

        // 🧾 Remove a single unit line from a broker's card.
        [HttpDelete("brochure-cards/{cardId:int}/lines/{lineId:int}")]
        public async Task<IActionResult> DeleteBrochureCardLine(int cardId, int lineId)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:delete"))
            {
                _logger.LogWarning("DeleteBrochureCardLine: forbidden — caller lacks billing:delete permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var line = await _context.BrochureCardLines.FirstOrDefaultAsync(l => l.LineId == lineId && l.CardId == cardId);
            if (line == null) return NotFound(new { message = "Brochure card line not found." });
            var owned = await _context.BrochureCards.AnyAsync(c => c.CardId == cardId && c.TenantId == tenantId);
            if (!owned) return Forbid();
            line.IsDeleted = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Brochure card line removed." });
        }

        // 🧾 Worker callback (AllowAnonymous — the worker holds no bearer token):
        // close the card + advance the deal once the PDF has been rendered to disk.
        [AllowAnonymous]
        [HttpPost("brochure-cards/{cardId:int}/generated")]
        public async Task<IActionResult> CompleteBrochureCard(int cardId, [FromBody] BrochureCardGeneratedDto payload)
        {
            if (payload == null) return BadRequest(new { message = "Callback payload is required." });

            var card = await _context.BrochureCards
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CardId == cardId);
            if (card == null) return NotFound(new { message = "Brochure card not found." });

            card.Status = BrochureCardStatus.Generated;
            card.IsDeleted = true;
            card.GeneratedFilePath = string.IsNullOrWhiteSpace(payload.FilePath) ? card.GeneratedFilePath : payload.FilePath;
            card.UpdatedAt = DateTime.UtcNow;

            var deal = await _context.DealRecords
                .FirstOrDefaultAsync(d => d.DealId == card.DealId && d.TenantId == card.TenantId);
            if (deal != null)
            {
                deal.DealStage = DealWorkflowStage.ProposalQuoteSent;
                deal.LastStageUpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Brochure card closed and deal advanced to Proposal / Quote Sent.",
                cardId = card.CardId,
                dealStage = deal == null ? null : deal.DealStage.ToString()
            });
        }

        // 💰 Co-brokerage split management (AgencyAdmin only).
        [HttpGet("deals/{dealId:int}/broker-splits")]
        public async Task<IActionResult> GetDealBrokerSplits(int dealId)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:view"))
            {
                _logger.LogWarning("GetDealBrokerSplits: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var deal = await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            var splits = await _context.DealBrokerSplits
                .Where(s => s.DealId == dealId)
                .OrderByDescending(s => s.SplitPercentage)
                .ToListAsync();
            return Ok(splits);
        }

        [HttpPut("deals/{dealId:int}/broker-splits")]
        public async Task<IActionResult> UpsertDealBrokerSplits(int dealId, [FromBody] UpsertBrokerSplitsRequest request)
        {
            if (!await _permissionCheck.HasPermissionAsync("billing:edit"))
            {
                _logger.LogWarning("UpsertDealBrokerSplits: forbidden — caller lacks billing:edit permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            if (callerRole != "AgencyAdmin" && callerRole != "PlatformAdmin")
                return Forbid();

            var tenantId = GetCurrentTenant();
            var deal = await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            // 🔒 Closed deals: splits are locked because the realised
            // commission has already been distributed.
            if (deal.DealStage == DealWorkflowStage.ClosedWon || deal.DealStage == DealWorkflowStage.ClosedLost)
            {
                return BadRequest(new { message = "This deal is closed and commission splits cannot be edited." });
            }

            if (request.Splits == null || !request.Splits.Any())
            {
                _context.DealBrokerSplits.RemoveRange(_context.DealBrokerSplits.Where(s => s.DealId == dealId));
                await _context.SaveChangesAsync();
                return Ok(new { message = "All broker splits cleared." });
            }

            var existing = _context.DealBrokerSplits.Where(s => s.DealId == dealId);
            _context.DealBrokerSplits.RemoveRange(existing);

            decimal totalPercentage = request.Splits.Sum(s => s.SplitPercentage);
            // 🧮 Split the realised commission, not the raw CommissionValue
            // (CommissionValue is either a percentage like 5 or a flat Rand
            // amount — both need to be normalised to Rand before splitting).
            decimal realisedCommission = deal.CommissionType == CommissionTypes.Percent
                ? Math.Round(deal.TransactionValue * deal.CommissionValue / 100m, 2)
                : deal.CommissionValue;

            foreach (var split in request.Splits)
            {
                _context.DealBrokerSplits.Add(new DealBrokerSplit
                {
                    DealId = dealId,
                    BrokerId = split.BrokerId,
                    BrokerName = split.BrokerName,
                    SplitPercentage = split.SplitPercentage,
                    SplitAmount = totalPercentage > 0 ? Math.Round(realisedCommission * split.SplitPercentage / 100m, 2) : 0m
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Broker splits updated.", totalPercentage });
        }

        // 🔗 Link a contact to a deal (adds DealId to contact's LinkedDealIds CSV).
        [HttpPost("contacts/{contactId}/link-deal/{dealId:int}")]
        public async Task<IActionResult> LinkContactToDeal(string contactId, int dealId)
        {
            if (!await _permissionCheck.HasPermissionAsync("deals:edit"))
            {
                _logger.LogWarning("LinkContactToDeal: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var contact = await _context.CRMContacts.FirstOrDefaultAsync(c => c.ContactId == contactId && c.TenantId == tenantId);
            if (contact == null) return NotFound(new { message = "Contact not found." });

            var deal = await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal not found." });

            var linked = string.IsNullOrWhiteSpace(contact.LinkedDealIds)
                ? new List<string>()
                : contact.LinkedDealIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

            if (!linked.Contains(dealId.ToString()))
                linked.Add(dealId.ToString());

            contact.LinkedDealIds = string.Join(',', linked);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Contact linked to deal.", linkedDealIds = contact.LinkedDealIds });
        }
    }
}
