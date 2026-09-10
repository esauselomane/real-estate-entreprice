using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
using CRM.Microservice.Data;
using CRM.Microservice.DTOs;
using CRM.Microservice.Models;
using CRM.Microservice.Services.Abstractions;
using CRM.Microservice.Services;

namespace CRM.Microservice.Controllers
{
    [ApiController]
    [Route("api/crm")]
    [Authorize]
    public class CRMController : CRMControllerBase
    {
        public CRMController(
            CRMDbContext dbContext,
            IHttpClientFactory http,
            IConfiguration configuration,
            ILogger<CRMController> logger,
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
            : base(dbContext, http, configuration, logger, permissionCheck,
                agencyRepository, brokerRepository, leadRepository, dealRepository,
                companyRepository, legalEntityRepository, contactRepository,
                geographyRepository, operationalRepository, brochureCardRepository,
                brochureCardLineRepository, dealBrokerSplitRepository, notificationRepository, brokerTargetRepository)
        {
        }

        [HttpGet("operations")]
        public async Task<IActionResult> GetOperationalRecords([FromQuery] int page = 1, [FromQuery] string? module = null, [FromQuery] int? propertyId = null, [FromQuery] int? propertyUnitId = null, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("units:view"))
            {
                Logger.LogWarning("GetOperationalRecords: forbidden — caller lacks units:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<OperationalRecord> query = OperationalRepository.Query();
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

            if (!await PermissionCheck.HasPermissionAsync(permission))
            {
                Logger.LogWarning("CreateOperationalRecord: forbidden — caller lacks {Permission} permission", permission);
                return Forbid();
            }

            record.CreatedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
            OperationalRepository.Add(record);
            await DbContext.SaveChangesAsync();
            return Ok(record);
        }

        [HttpPut("operations/{recordId:int}")]
        public async Task<IActionResult> UpdateOperationalRecord(int recordId, [FromBody] OperationalRecord updatedRecord)
        {
            var record = await OperationalRepository.GetByIdAsync(recordId);
            if (record == null) return NotFound(new { message = "Operational record not found." });

            var permission = (record.Module ?? string.Empty).ToLowerInvariant() switch
            {
                "leases" or "maintenance" => "units:edit",
                "valuations" => "valuations:bypass",
                _ => "properties:edit"
            };

            if (!await PermissionCheck.HasPermissionAsync(permission))
            {
                Logger.LogWarning("UpdateOperationalRecord: forbidden — caller lacks {Permission} permission", permission);
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
            await DbContext.SaveChangesAsync();
            return Ok(record);
        }
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts([FromQuery] int page, [FromQuery] string? tenantId, [FromQuery] string? contactType, [FromQuery] decimal? budgetMin, [FromQuery] decimal? budgetMax, [FromQuery] string? area)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("GetContacts: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<Contact> query = ContactRepository.Query()
                .AsNoTracking()
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails);
            if (visibleTenants.Count > 0)
                query = query.Where(c => visibleTenants.Contains(c.TenantId));
            if (!string.IsNullOrEmpty(contactType))
            {
                var typeParts = contactType.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var parsedTypes = typeParts
                    .Where(p => Enum.TryParse<ContactType>(p, true, out _))
                    .Select(p => Enum.Parse<ContactType>(p, true))
                    .ToList();
                if (parsedTypes.Any())
                {
                    query = query.Where(c => parsedTypes.Any(t => c.ContactType.HasFlag(t)));
                }
            }
            if (budgetMin.HasValue)
                query = query.Where(c => c.BudgetLimit >= budgetMin.Value);
            if (budgetMax.HasValue)
                query = query.Where(c => c.BudgetLimit <= budgetMax.Value);
            if (!string.IsNullOrWhiteSpace(area))
            {
                var areaTerm = area.Trim().ToLower();
                query = query.Where(c => c.PrimaryLocation != null && c.PrimaryLocation.ToLower().Contains(areaTerm));
            }
            var ordered = query.OrderBy(c => c.FullName);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["contactType"] = await AggregateStringAsync(ordered, c => c.ContactType.ToString()),
                ["position"] = await AggregateStringAsync(ordered, c => c.PositionJobTitle ?? string.Empty),
                ["hasCompany"] = await AggregateBoolAsync(ordered, c => !string.IsNullOrEmpty(c.AssociatedCompanyId))
            };
            var (items, total) = await PageAsync(ordered, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }
        [HttpPost("contacts/search")]
        public async Task<IActionResult> SearchContacts([FromBody] ContactSearchDto request)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("SearchContacts: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(request.TenantId);
            IQueryable<Contact> query = ContactRepository.Query()
                .AsNoTracking()
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails);
            if (visibleTenants.Count > 0)
                query = query.Where(c => visibleTenants.Contains(c.TenantId));
            if (!string.IsNullOrEmpty(request.ContactType))
            {
                var typeParts = request.ContactType.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var parsedTypes = typeParts
                    .Where(p => Enum.TryParse<ContactType>(p, true, out _))
                    .Select(p => Enum.Parse<ContactType>(p, true))
                    .ToList();
                if (parsedTypes.Any())
                    query = query.Where(c => parsedTypes.Any(t => c.ContactType.HasFlag(t)));
            }
            if (request.BudgetMin.HasValue)
                query = query.Where(c => c.BudgetLimit >= request.BudgetMin.Value);
            if (request.BudgetMax.HasValue)
                query = query.Where(c => c.BudgetLimit <= request.BudgetMax.Value);
            if (!string.IsNullOrWhiteSpace(request.Area))
            {
                var areaTerm = request.Area.Trim().ToLower();
                query = query.Where(c => c.PrimaryLocation != null && c.PrimaryLocation.ToLower().Contains(areaTerm));
            }
            if (request.AreaIds != null && request.AreaIds.Any())
            {
                var areaIdStrings = request.AreaIds.Select(id => id.ToString()).ToList();
                query = query.Where(c => c.AreaInterestIds != null && areaIdStrings.Any(s => c.AreaInterestIds.Contains(s)));
            }
            var ordered = query.OrderBy(c => c.FullName);
            var aggregates = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
            {
                ["contactType"] = await AggregateStringAsync(ordered, c => c.ContactType.ToString()),
                ["position"] = await AggregateStringAsync(ordered, c => c.PositionJobTitle ?? string.Empty),
                ["hasCompany"] = await AggregateBoolAsync(ordered, c => !string.IsNullOrEmpty(c.AssociatedCompanyId))
            };
            var (items, total) = await PageAsync(ordered, GetPage(request.Page));
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            HttpContext.Response.Headers["X-Aggregates"] = SerializeAggregates(aggregates);
            return Ok(items);
        }
        [HttpGet("geography")]
        public async Task<IActionResult> SearchGeography([FromQuery] string? search)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("SearchGeography: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var query = GeographyRepository.Query();
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
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("GetGeography: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var geo = await GeographyRepository.GetByIdAsync(geographyId);
            return geo == null ? NotFound(new { message = "Geography record not found." }) : Ok(geo);
        }
        [HttpPost("contacts")]
        public async Task<IActionResult> CreateContact([FromBody] Contact contact)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:create"))
            {
                Logger.LogWarning("CreateContact: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            if (contact == null || string.IsNullOrWhiteSpace(contact.FullName))
                return BadRequest(new { message = "Contact details are required." });

            if (string.IsNullOrEmpty(contact.ContactId))
                contact.ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            contact.TenantId = GetCurrentTenant();
            contact.CreatedAt = DateTime.UtcNow;

            if (string.IsNullOrEmpty(contact.AssociatedLegalEntityId) && !string.IsNullOrEmpty(contact.AssociatedCompanyId))
            {
                var co = await CompanyRepository.Query().FirstOrDefaultAsync(c => c.CompanyId == contact.AssociatedCompanyId);
                if (co?.AssociatedLegalEntityId != null)
                    contact.AssociatedLegalEntityId = co.AssociatedLegalEntityId;
            }

            ContactRepository.Add(contact);
            await DbContext.SaveChangesAsync();
            return Ok(contact);
        }

        [HttpPut("contacts/{contactId}")]
        public async Task<IActionResult> UpdateContact(string contactId, [FromBody] Contact updatedContact)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:edit"))
            {
                Logger.LogWarning("UpdateContact: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var existing = await ContactRepository.GetByIdAsync(contactId);
            if (existing == null) return NotFound(new { message = "Contact record not found." });

            existing.ContactType = updatedContact.ContactType;
            existing.FullName = updatedContact.FullName;
            existing.CompanyName = updatedContact.CompanyName;
            existing.EmailAddress = updatedContact.EmailAddress;
            existing.PhoneNumber = updatedContact.PhoneNumber;
            existing.PositionJobTitle = updatedContact.PositionJobTitle;
            existing.PrimaryLocation = updatedContact.PrimaryLocation;
            existing.BudgetLimit = updatedContact.BudgetLimit;
            existing.PropertyTypeInterests = updatedContact.PropertyTypeInterests;
            existing.AreaInterestIds = updatedContact.AreaInterestIds;
            existing.Notes = updatedContact.Notes;
            existing.AssociatedCompanyId = updatedContact.AssociatedCompanyId;
            existing.AssociatedLegalEntityId = updatedContact.AssociatedLegalEntityId;
            existing.AgencyId = updatedContact.AgencyId;
            existing.IsAgencyAdmin = updatedContact.IsAgencyAdmin;

            await DbContext.SaveChangesAsync();
            return Ok(existing);
        }
        [HttpGet("agencies")]
        public async Task<IActionResult> GetAgencies([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("GetAgencies: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            Logger.LogInformation("GetAgencies: Page={Page} Role={Role} Override={Override} VisibleTenants={TenantCount}",
                page, callerRole, tenantId ?? "(default)", visibleTenants.Count);

            IQueryable<AgencyRecord> query = AgencyRepository.Query();
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
            Logger.LogInformation("GetAgencies: Returned {Count} agencies (page {Page}) out of {Total} total",
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
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("GetAgenciesRoster: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<AgencyRecord> query = AgencyRepository.Query();
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
            Logger.LogInformation("GetAgenciesRoster: returned {Count} agencies for override {Override}",
                roster.Count, tenantId ?? "(default)");
            return Ok(roster);
        }
        [HttpPost("agencies")]
        public async Task<IActionResult> CreateAgency([FromBody] AgencyRecord agency)
        {
            var caller = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
            if (!await PermissionCheck.HasPermissionAsync("agencies:create"))
            {
                Logger.LogWarning("CreateAgency: forbidden — caller {Caller} lacks agencies:create permission", caller);
                return Forbid();
            }

            if (GetCurrentCanonicalRole() != "PlatformAdmin")
            {
                Logger.LogWarning("CreateAgency: forbidden — caller {Caller} role {Role} cannot provision agencies",
                    caller, GetCurrentCanonicalRole());
                return Forbid();
            }

            if (agency == null || string.IsNullOrWhiteSpace(agency.Name))
            {
                Logger.LogWarning("CreateAgency: rejected — missing agency name, caller={Caller}", caller);
                return BadRequest(new { message = "Agency name is required." });
            }

            if (string.IsNullOrWhiteSpace(agency.AgencyId))
                agency.AgencyId = "TEN-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            agency.TenantId = GetCurrentTenant();
            agency.CreatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(agency.ParentAgencyId))
            {
                var parentExists = await AgencyRepository.Query().AnyAsync(a => a.AgencyId == agency.ParentAgencyId);
                if (!parentExists)
                {
                    Logger.LogWarning("CreateAgency: rejected — ParentAgencyId={ParentId} not found, caller={Caller}", agency.ParentAgencyId, caller);
                    return BadRequest(new { message = $"ParentAgencyId '{agency.ParentAgencyId}' not found." });
                }
            }

            AgencyRepository.Add(agency);
            await DbContext.SaveChangesAsync();

            Logger.LogInformation("CreateAgency: created AgencyId={AgencyId} Name={Name} Tenant={Tenant} Parent={Parent} By={Caller}",
                agency.AgencyId, agency.Name, agency.TenantId, agency.ParentAgencyId ?? "(root)", caller);
            return Ok(agency);
        }

        [HttpPut("agencies/{agencyId}")]
        public async Task<IActionResult> UpdateAgency(string agencyId, [FromBody] AgencyRecord updatedAgency)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:edit"))
            {
                Logger.LogWarning("UpdateAgency: forbidden — caller lacks agencies:edit permission");
                return Forbid();
            }

            var agency = await AgencyRepository.Query()
                .Include(a => a.Contacts)
                .FirstOrDefaultAsync(a => a.AgencyId == agencyId);
            if (agency == null) return NotFound(new { message = "Agency record not found." });
            agency.Name = updatedAgency.Name;
            agency.TradingName = updatedAgency.TradingName;
            agency.RegistrationNumber = updatedAgency.RegistrationNumber;
            agency.CompanyLogoUrl = updatedAgency.CompanyLogoUrl;
            agency.ContactEmail = updatedAgency.ContactEmail;
            agency.HeadquartersAddress = updatedAgency.HeadquartersAddress;
            agency.OperationalStatus = updatedAgency.OperationalStatus;
            agency.BrokerageLicense = updatedAgency.BrokerageLicense;
            agency.IsTrial = updatedAgency.IsTrial;
            agency.TrialStartDate = updatedAgency.TrialStartDate;
            agency.TrialEndDate = updatedAgency.TrialEndDate;
            if (!string.IsNullOrWhiteSpace(updatedAgency.ParentAgencyId))
            {
                if (updatedAgency.ParentAgencyId == agencyId)
                    return BadRequest(new { message = "An agency cannot be its own parent." });
                var parentExists = await AgencyRepository.Query().AnyAsync(a => a.AgencyId == updatedAgency.ParentAgencyId);
                if (!parentExists)
                    return BadRequest(new { message = $"ParentAgencyId '{updatedAgency.ParentAgencyId}' not found." });
                agency.ParentAgencyId = updatedAgency.ParentAgencyId;
            }
            await DbContext.SaveChangesAsync();
            return Ok(agency);
        }

        // 📇 Agency-scoped contacts (staff contacts bound to an agency).
        [HttpGet("agencies/{agencyId}/contacts")]
        public async Task<IActionResult> GetAgencyContacts(string agencyId)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("GetAgencyContacts: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var exists = await AgencyRepository.Query().AnyAsync(a => a.AgencyId == agencyId);
            if (!exists) return NotFound(new { message = "Agency not found." });
            var contacts = await ContactRepository.Query()
                .Where(c => c.AgencyId == agencyId)
                .OrderBy(c => c.FullName).ToListAsync();
            return Ok(contacts);
        }

        [HttpPost("agencies/{agencyId}/contacts")]
        public async Task<IActionResult> CreateAgencyContact(string agencyId, [FromBody] Contact contact)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:create"))
            {
                Logger.LogWarning("CreateAgencyContact: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            var agency = await AgencyRepository.Query().FirstOrDefaultAsync(a => a.AgencyId == agencyId);
            if (agency == null) return NotFound(new { message = "Agency not found." });
            if (string.IsNullOrWhiteSpace(contact.FullName))
                return BadRequest(new { message = "Contact full name is required." });

            contact.ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            contact.AgencyId = agencyId;
            contact.CreatedAt = DateTime.UtcNow;
            ContactRepository.Add(contact);
            await DbContext.SaveChangesAsync();
            return Ok(contact);
        }
        [HttpDelete("contacts/{contactId}")]
        public async Task<IActionResult> DeleteContact(string contactId)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:delete"))
            {
                Logger.LogWarning("DeleteContact: forbidden — caller lacks users:delete permission");
                return Forbid();
            }

            var existing = await ContactRepository.GetByIdAsync(contactId);
            if (existing == null) return NotFound(new { message = "Contact not found." });
            ContactRepository.Remove(existing);
            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Contact removed." });
        }

        // 🚀 Promote an agency staff contact into a Broker record.
        [HttpPost("contacts/{contactId}/promote-to-broker")]
        public async Task<IActionResult> PromoteContactToBroker(string contactId)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:edit"))
            {
                Logger.LogWarning("PromoteContactToBroker: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var contact = await ContactRepository.Query().FirstOrDefaultAsync(c => c.ContactId == contactId);
            if (contact == null) return NotFound(new { message = "Contact not found." });

            var broker = new BrokerRecord
            {
                BrokerId = "BRK-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                TenantId = GetCurrentTenant(),
                AgencyId = contact.AgencyId,
                FullName = contact.FullName,
                Email = contact.EmailAddress,
                Phone = contact.PhoneNumber,
                Role = contact.IsAgencyAdmin ? "Manager" : "Broker",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            BrokerRepository.Add(broker);
            contact.ContactType = ContactType.Broker;
            await DbContext.SaveChangesAsync();
            return Ok(new { broker, contact = contact });
        }

        [HttpGet("brokers")]
        public async Task<IActionResult> GetBrokers([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("GetBrokers: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<BrokerRecord> query = BrokerRepository.Query();
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
            if (!await PermissionCheck.HasPermissionAsync("users:create"))
            {
                Logger.LogWarning("CreateBroker: forbidden — caller lacks users:create permission");
                return Forbid();
            }

            if (broker == null || string.IsNullOrWhiteSpace(broker.FullName))
                return BadRequest(new { message = "Broker details are required." });

             if (string.IsNullOrWhiteSpace(broker.BrokerId))
                broker.BrokerId = "BRK-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            broker.TenantId = GetCurrentTenant();
            broker.CreatedAt = DateTime.UtcNow;
            BrokerRepository.Add(broker);
            await DbContext.SaveChangesAsync();
            return Ok(broker);
        }

        [HttpPut("brokers/{brokerId}")]
        public async Task<IActionResult> UpdateBroker(string brokerId, [FromBody] BrokerRecord updatedBroker)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:edit"))
            {
                Logger.LogWarning("UpdateBroker: forbidden — caller lacks users:edit permission");
                return Forbid();
            }

            var broker = await BrokerRepository.GetByIdAsync(brokerId);
            if (broker == null) return NotFound(new { message = "Broker record not found." });
            broker.AgencyId = updatedBroker.AgencyId;
            broker.FullName = updatedBroker.FullName;
            broker.Email = updatedBroker.Email;
            broker.Phone = updatedBroker.Phone;
            broker.Role = updatedBroker.Role;
            broker.Status = updatedBroker.Status;
            await DbContext.SaveChangesAsync();
            return Ok(broker);
        }
        [HttpGet("contacts/{contactId}")]
        public async Task<IActionResult> GetContactById([FromRoute] string contactId, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("users:view"))
            {
                Logger.LogWarning("GetContactById: forbidden — caller lacks users:view permission");
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(contactId))
            {
                return BadRequest(new { message = "Contact id is required." });
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);

            var contact = await ContactRepository.Query()
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails)
                .FirstOrDefaultAsync(c => c.ContactId == contactId);

            if (contact == null)
            {
                Logger.LogInformation("GetContactById: ContactId={ContactId} not found", contactId);
                return NotFound(new { message = "Contact record not found." });
            }

            if (visibleTenants.Count > 0 && !visibleTenants.Contains(contact.TenantId))
            {
                Logger.LogWarning("GetContactById: ContactId={ContactId} outside visible tenants", contactId);
                return Forbid();
            }

            return Ok(contact);
        }
        [HttpGet("boards")]
        public async Task<IActionResult> GetCRMBoards([FromQuery] int page, [FromQuery] string agentName = "", [FromQuery] string userRole = "", [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:view"))
            {
                Logger.LogWarning("GetCRMBoards: forbidden — caller lacks deals:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            // 🔐 Scoping is authoritative from the JWT, never from a client-supplied role string.
            var callerRole = GetCurrentCanonicalRole();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? agentName ?? string.Empty;

            IQueryable<LeadRecord> leadsQuery = LeadRepository.Query().Where(l => !l.IsConvertedToDeal);
            IQueryable<DealRecord> dealsQuery = DealRepository.Query();
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
        [HttpGet("report/summary")]
        public async Task<IActionResult> GetPipelineReportSummary([FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetPipelineReportSummary: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();

            IQueryable<LeadRecord> baseLeadQuery = LeadRepository.Query()
                .Where(l => !l.IsConvertedToDeal)
                .Where(l => !isBroker || l.AllocatedBrokerIds == callerId || l.AllocatedBrokerIds.StartsWith(callerId + ",") || l.AllocatedBrokerIds.Contains("," + callerId));
            IQueryable<DealRecord> baseDealQuery = DealRepository.Query()
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

            Logger.LogInformation("GetPipelineReportSummary: overrideTenant={Override} effectiveTenants={TenantCount} | ActiveLeads={Active} Valuations={Valuations} Conveyancing={Conveyancing} ClosedWon={ClosedWon} PipelineValue={PipelineValue}",
                tenantId ?? "(default)", visibleTenants.Count, summaryReport.ActiveLeadsCount, summaryReport.ValuationsInProcessCount, summaryReport.ConveyancingAtDeedsCount, summaryReport.FinalizedClosedCount, summaryReport.TotalPipelineEstimatedValue);

            return Ok(summaryReport);
        }

        [HttpGet("report/overview")]
        public async Task<IActionResult> GetReportOverview([FromQuery] int? year, [FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetReportOverview: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();
            var activeYear = year ?? DateTime.UtcNow.Year;
            Logger.LogInformation("GetReportOverview: Year={Year} overrideTenant={Override} Tenants={TenantCount}", activeYear, tenantId ?? "(default)", visibleTenants.Count);

            var yearStart = new DateTime(activeYear, 1, 1);
            var yearEnd = new DateTime(activeYear + 1, 1, 1);

            IQueryable<LeadRecord> tenantLeadsQuery = LeadRepository.Query()
                .Where(l => l.CreatedAt >= yearStart && l.CreatedAt < yearEnd)
                .Where(l => !isBroker || l.AllocatedBrokerIds == callerId || l.AllocatedBrokerIds.StartsWith(callerId + ",") || l.AllocatedBrokerIds.Contains("," + callerId));
            IQueryable<DealRecord> tenantDealsQuery = DealRepository.Query()
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
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetCommissionByStage: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();

            IQueryable<DealRecord> query = DealRepository.Query()
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
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetBrokerLeaderboard: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var callerId = GetCurrentUserId();
            IQueryable<DealRecord> query = DealRepository.Query()
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
            var brokerRoster = await BrokerRepository.Query()
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
        [HttpGet("report/broker-metrics")]
        public async Task<IActionResult> GetBrokerMetrics([FromQuery] string? tenantId, [FromQuery] string? brokerId = null, [FromQuery] string? month = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
                return Forbid();

            var tenantIds = await GetVisibleTenantIdsAsync(tenantId);
            if (tenantIds.Count == 0) return Ok(new { gci = 0m, companyDollar = 0m, activeListings = 0, averageDom = 0, monthlyTrend = new object[0], dealCount = 0, closedWonCount = 0 });

            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();

            // 🔐 Broker scope: if the caller IS a broker and didn't pass a
            // brokerId, pin to themselves. Admins with no brokerId see the
            // tenant aggregate.
            string? effectiveBrokerId = brokerId;
            if (role == "Broker")
            {
                if (!string.IsNullOrWhiteSpace(brokerId) && brokerId != callerId)
                    return Forbid();
                effectiveBrokerId = callerId;
            }

            // Base query: deals in scope, optionally filtered to one broker.
            var deals = DealRepository.Query()
                .Where(d => tenantIds.Contains(d.TenantId)
                    && (effectiveBrokerId == null || d.AllocatedBrokerIds == effectiveBrokerId || d.AllocatedBrokerIds.Contains(effectiveBrokerId + ",")
                        || d.AllocatedBrokerIds.Contains("," + effectiveBrokerId)
                        || d.AllocatedBrokerIds == effectiveBrokerId));

            // GCI = sum of commissionValue (Percent: txn × pct/100, Amount: fixed)
            // over all deals in scope.
            var gci = await deals.SumAsync(d => (decimal?)(
                d.CommissionType == CommissionTypes.Percent
                    ? d.TransactionValue * d.CommissionValue / 100m
                    : d.CommissionValue)) ?? 0m;

            // Company Dollar = GCI × the configured split (default 70% / 30%)
            // for now: companyDollar = 70% of GCI (broker split is configurable
            // per-deal but the system default is 30% broker / 70% brokerage).
            var companyDollar = Math.Round(gci * 0.7m, 2);

            // Active listings: call the property service to count
            // for-sale + for-rent properties in the visible tenant.
            // The CRM service doesn't share a DB with property, so we
            // call over HTTP and ask for a page-size=1 with a huge
            // pageSize just to read X-Total-Count.
            int activeListings = 0;
            double averageDom = 0;
            try
            {
                var propertyBaseUrl = HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["Services:PropertyBaseUrl"]
                    ?? "http://localhost:5000/gateway";
                using var http = new HttpClient { BaseAddress = new Uri(propertyBaseUrl) };
                var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader))
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authHeader.Replace("Bearer ", ""));
                var tenantList = string.Join(',', tenantIds);
                var resp = await http.GetAsync($"/properties/active-listings-count?tenantId={Uri.EscapeDataString(tenantList)}"
                    + (effectiveBrokerId != null ? $"&linkedBrokerId={Uri.EscapeDataString(effectiveBrokerId)}" : ""));
                if (resp.IsSuccessStatusCode)
                {
                    var raw = await resp.Content.ReadAsStringAsync();
                    var obj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw);
                    activeListings = obj.GetProperty("activeListings").GetInt32();
                    averageDom = obj.GetProperty("averageDom").GetDouble();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "GetBrokerMetrics: could not fetch active-listings count from property service");
            }

            // Monthly trend (last 6 months by default, or a specific month if provided).
            DateTime? targetMonth = null;
            if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParseExact(month, "yyyy-MM", null, System.Globalization.DateTimeStyles.None, out var parsedMonth))
                targetMonth = parsedMonth;

            int monthsToLookBack = targetMonth.HasValue ? 0 : 5;
            var startDate = DateTime.UtcNow.AddMonths(-monthsToLookBack);
            var trend = await deals
                .Where(d => d.CreatedAt >= new DateTime(startDate.Year, startDate.Month, 1))
                .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Gci = g.Sum(d => d.CommissionType == CommissionTypes.Percent
                        ? d.TransactionValue * d.CommissionValue / 100m
                        : d.CommissionValue),
                    ClosedCount = g.Count(d => d.DealStage == DealWorkflowStage.ClosedWon)
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            // If a specific month was requested, filter the trend to just that month.
            if (targetMonth.HasValue)
            {
                trend = trend.Where(t => t.Year == targetMonth.Value.Year && t.Month == targetMonth.Value.Month).ToList();
            }
            var monthlyTrend = trend.Select(t => new
            {
                period = $"{t.Year}-{t.Month:D2}",
                gci = Math.Round(t.Gci, 2),
                companyDollar = Math.Round(t.Gci * 0.7m, 2),
                closedCount = t.ClosedCount
            }).ToList();

            // Trend vs last month (for KPI trend badges).
            var thisMonthGci = trend.LastOrDefault()?.Gci ?? 0m;
            var lastMonthGci = trend.Count >= 2 ? trend[^2].Gci : 0m;
            var trendPct = lastMonthGci == 0m ? 0m : Math.Round((thisMonthGci - lastMonthGci) / lastMonthGci * 100m, 1);

            // 📊 Monthly target + progress: fetch up to 6 months of target entries
            // for the effective broker, then merge with trend GCI to compute
            // progress percentage for the gauge chart.
            int lookbackMonths = targetMonth.HasValue ? 0 : 5;
            var targetStartMonth = DateTime.UtcNow.AddMonths(-lookbackMonths);
            var targetYear = targetMonth?.Year ?? 0;
            var targetMonthNum = targetMonth?.Month ?? 0;

            IQueryable<BrokerMonthlyTarget> targetQuery = BrokerTargetRepository.Query();
            if (effectiveBrokerId != null)
                targetQuery = targetQuery.Where(t => t.BrokerId == effectiveBrokerId);
            else
                targetQuery = targetQuery.Where(t => t.BrokerId == callerId);

            if (targetMonthNum > 0)
            {
                targetQuery = targetQuery.Where(t => t.Year == targetYear && t.Month == targetMonthNum);
            }
            else
            {
                var ym = new DateTime(targetStartMonth.Year, targetStartMonth.Month, 1);
                targetQuery = targetQuery.Where(t => t.CreatedAt >= ym);
            }

            var targets = await targetQuery
                .Select(t => new
                {
                    t.Year,
                    t.Month,
                    Target = t.TargetCommission
                })
                .ToListAsync();

            var targetProgress = targets.Select(t => {
                var monthTrend = trend.FirstOrDefault(mt => mt.Year == t.Year && mt.Month == t.Month);
                var realised = monthTrend?.Gci ?? 0m;
                return new
                {
                    year = t.Year,
                    month = t.Month,
                    period = $"{t.Year}-{t.Month:D2}",
                    target = Math.Round(t.Target, 2),
                    realised = Math.Round(realised, 2),
                    progress = t.Target == 0m ? 0m : Math.Round(realised / t.Target * 100m, 1)
                };
            }).ToList();

            return Ok(new
            {
                gci = Math.Round(gci, 2),
                companyDollar = Math.Round(companyDollar, 2),
                activeListings,
                averageDom,
                monthlyTrend,
                gciTrendPct = trendPct,
                dealCount = await deals.CountAsync(),
                closedWonCount = await deals.CountAsync(d => d.DealStage == DealWorkflowStage.ClosedWon),
                monthlyTargets = targetProgress
            });
        }

        // 📊 GET broker monthly targets (historical entries). For brokers, returns
        // only their own targets. For admins, can pass brokerId to filter.
        [HttpGet("broker-targets")]
        public async Task<IActionResult> GetBrokerTargets(
            [FromQuery] string? brokerId = null,
            [FromQuery] int? year = null,
            [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
                return Forbid();

            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            var effectiveBrokerId = brokerId;

            if (role == "Broker")
            {
                effectiveBrokerId = callerId;
            }

            var query = BrokerTargetRepository.Query();
            if (!string.IsNullOrEmpty(effectiveBrokerId))
                query = query.Where(t => t.BrokerId == effectiveBrokerId);
            if (year.HasValue)
                query = query.Where(t => t.Year == year.Value);

            var targets = await query
                .Select(t => new
                {
                    id = t.Id,
                    brokerId = t.BrokerId,
                    year = t.Year,
                    month = t.Month,
                    targetCommission = t.TargetCommission,
                    createdAt = t.CreatedAt,
                    updatedAt = t.UpdatedAt
                })
                .OrderByDescending(t => t.year)
                .ThenByDescending(t => t.month)
                .ToListAsync();

            return Ok(targets);
        }

        // 📊 POST set a broker's monthly target (upsert for a given year+month).
        // Brokers can only set targets for themselves. Admins can set for any broker.
        [HttpPost("broker-targets")]
        public async Task<IActionResult> SetBrokerTarget([FromBody] SetBrokerMonthlyTargetRequest request)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:edit"))
                return Forbid();

            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            var tenantId = GetCurrentTenant();

            string effectiveBrokerId = request.BrokerId ?? callerId;
            if (role == "Broker" && effectiveBrokerId != callerId)
                return Forbid();

            if (request.TargetCommission < 0)
                return BadRequest(new { message = "Target commission must be non-negative." });

            var existing = await BrokerTargetRepository.Query()
                .FirstOrDefaultAsync(t => t.BrokerId == effectiveBrokerId && t.Year == request.Year && t.Month == request.Month);

            if (existing == null)
            {
                var target = new BrokerMonthlyTarget
                {
                    BrokerId = effectiveBrokerId,
                    TenantId = tenantId,
                    Year = request.Year,
                    Month = request.Month,
                    TargetCommission = request.TargetCommission
                };
                BrokerTargetRepository.Add(target);
            }
            else
            {
                existing.TargetCommission = request.TargetCommission;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Broker target saved." });
        }

        // 📊 Activity log: recent deal stage changes for a specific broker.
        // Returns the last {limit} entries (default 10). Each entry includes
        // the deal stage transition timestamp so the frontend can poll
        // "since" the last entry seen.
        [HttpGet("broker-activity")]
        public async Task<IActionResult> GetBrokerActivity(
            [FromQuery] string? brokerId = null,
            [FromQuery] string? tenantId = null,
            [FromQuery] int limit = 10,
            [FromQuery] DateTime? since = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:view"))
                return Forbid();

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var role = GetCurrentCanonicalRole();

            string? effectiveBrokerId = brokerId;
            if (role == "Broker")
            {
                if (!string.IsNullOrWhiteSpace(brokerId) && brokerId != GetCurrentUserId())
                    return Forbid();
                effectiveBrokerId = GetCurrentUserId();
            }

            if (string.IsNullOrWhiteSpace(effectiveBrokerId))
            {
                Logger.LogWarning("GetBrokerActivity: no brokerId provided and caller is not a broker");
                return BadRequest(new { message = "brokerId is required for non-broker callers." });
            }

            var bid = effectiveBrokerId;
            IQueryable<DealRecord> query = DealRepository.Query()
                .Where(d => d.TenantId == visibleTenants.FirstOrDefault()
                    && (d.AllocatedBrokerIds == bid
                        || d.AllocatedBrokerIds.StartsWith(bid + ",")
                        || d.AllocatedBrokerIds.Contains("," + bid)));

            if (since.HasValue)
                query = query.Where(d => d.LastStageUpdatedAt > since.Value);

            var activities = await query
                .OrderByDescending(d => d.LastStageUpdatedAt)
                .Take(limit)
                .Select(d => new
                {
                    DealId = d.DealId,
                    d.DealStage,
                    PreviousStage = d.PreviousStage,
                    NewStage = d.DealStage.ToString(),
                    StageName = d.DealStage.ToString(),
                    d.TransactionValue,
                    d.RealisedCommission,
                    BrokerId = bid,
                    BrokerName = "Pending", // resolved below if available
                    DealAddress = d.PropertyAddress.Length > 0 ? d.PropertyAddress : d.DealId.ToString(),
                    d.LastStageUpdatedAt,
                    d.CreatedAt
                })
                .ToListAsync();

            HttpContext.Response.Headers["X-New-Activity-Count"] = activities.Count.ToString();
            return Ok(activities);
        }

        // 📋 Agent performance leaderboard: list of every broker in the
        // active tenant with their active listing count, deals closed, and
        // total GCI generated. When a brokerId is given as a filter, the
        // leaderboard still returns ALL brokers but marks which one is
        // selected, so the admin can compare.
        [Authorize]
        [HttpGet("report/broker-leaderboard-v2")]
        public async Task<IActionResult> GetBrokerLeaderboardV2([FromQuery] string? tenantId, [FromQuery] string? brokerId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
                return Forbid();

            var tenantIds = await GetVisibleTenantIdsAsync(tenantId);
            if (tenantIds.Count == 0) return Ok(Array.Empty<object>());

            // Brokers in the visible tenant scope (Email is a soft proxy for "active").
            var brokers = await BrokerRepository.Query()
                .Where(b => tenantIds.Contains(b.TenantId) && b.Email != "")
                .Select(b => new { b.BrokerId, b.FullName, b.TenantId })
                .ToListAsync();

            // Aggregate per-broker deal metrics.
            var deals = await DealRepository.Query()
                .Where(d => tenantIds.Contains(d.TenantId))
                .Select(d => new { d.AllocatedBrokerIds, d.DealStage, d.CommissionType, d.CommissionValue, d.TransactionValue })
                .ToListAsync();

            var rows = brokers.Select(b =>
            {
                var mine = deals.Where(d => BrokerMatches(d.AllocatedBrokerIds, b.BrokerId)).ToList();
                var gci = mine.Sum(d => d.CommissionType == CommissionTypes.Percent
                    ? d.TransactionValue * d.CommissionValue / 100m
                    : d.CommissionValue);
                return new
                {
                    brokerId = b.BrokerId,
                    name = b.FullName,
                    activeListings = 0, // populated below from property service if we ever want to cross-call
                    dealsClosed = mine.Count(d => d.DealStage == DealWorkflowStage.ClosedWon),
                    totalGci = Math.Round(gci, 2)
                };
            })
            .OrderByDescending(r => r.totalGci)
            .ToList();

            return Ok(rows);
        }

        // Helper: check if the given brokerId appears in the comma-separated
        // AllocatedBrokerIds field. Used by the leaderboard + metrics.
        private static bool BrokerMatches(string allocatedBrokerIds, string brokerId)
        {
            if (string.IsNullOrEmpty(allocatedBrokerIds)) return false;
            return allocatedBrokerIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(id => string.Equals(id, brokerId, StringComparison.OrdinalIgnoreCase));
        }

        // 📥 Compliance inbox: high-urgency items the broker must action.
        // Two categories:
        //   1. Expiring mandates — properties in the tenant with an active
        //      sole mandate whose EndDate is within 14 days (or already past).
        //      We reach into the property service via the public SoleMandates
        //      table; the CRM service doesn't have direct access to that DB
        //      so we call the property service over HTTP.
        //   2. Awaiting broker signature — deals in the Negotiation stage
        //      (i.e. awaiting the broker to push them to ProposalQuoteSent).
        [Authorize]
        [HttpGet("compliance/inbox")]
        public async Task<IActionResult> GetComplianceInbox([FromQuery] string? tenantId, [FromQuery] string? brokerId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:view"))
                return Forbid();

            var tenantIds = await GetVisibleTenantIdsAsync(tenantId);
            if (tenantIds.Count == 0) return Ok(Array.Empty<object>());

            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            string? effectiveBrokerId = brokerId;
            if (role == "Broker")
            {
                if (!string.IsNullOrWhiteSpace(brokerId) && brokerId != callerId)
                    return Forbid();
                effectiveBrokerId = callerId;
            }

            var items = new List<object>();

            // (2) Awaiting-broker-signature: deals in Negotiation stage.
            // Filter broker-matching client-side because BrokerMatches uses string.Split
            // which EF cannot translate to SQL.
            var awaitingDeals = await DealRepository.Query()
                .Where(d => tenantIds.Contains(d.TenantId) && d.DealStage == DealWorkflowStage.Negotiation)
                .OrderBy(d => d.LastStageUpdatedAt)
                .Take(20)
                .ToListAsync();
            var awaiting = awaitingDeals
                .Where(d => effectiveBrokerId == null || BrokerMatches(d.AllocatedBrokerIds, effectiveBrokerId))
                .Select(d => new
                {
                    itemId = $"deal-{d.DealId}",
                    category = "compliance.awaiting_signature",
                    severity = "info",
                    title = $"Deal DL-{d.DealId} awaits broker signature",
                    body = $"Buyer: {d.BuyerClientId} • Seller: {d.SellerClientId} • R {d.TransactionValue.ToString("N2")}",
                    linkType = "deal",
                    linkId = d.DealId.ToString(),
                    icon = "📝"
                })
                .ToList();
            items.AddRange(awaiting);

            // (1) Expiring mandates: call the property service over HTTP.
            // We send a comma-separated list of tenantIds and the brokerId
            // so the property service can scope its sole-mandate query.
            try
            {
                var propertyBaseUrl = HttpContext.RequestServices
                    .GetRequiredService<IConfiguration>()["Services:PropertyBaseUrl"]
                    ?? "http://localhost:5002";
                using var http = new HttpClient { BaseAddress = new Uri(propertyBaseUrl) };
                var tenantList = string.Join(',', tenantIds);
                var url = $"/api/properties/expiring-mandates?tenantId={Uri.EscapeDataString(tenantList)}"
                    + (effectiveBrokerId != null ? $"&brokerId={Uri.EscapeDataString(effectiveBrokerId)}" : "")
                    + "&days=14";
                var resp = await http.GetAsync(url);
                if (resp.IsSuccessStatusCode)
                {
                    var raw = await resp.Content.ReadAsStringAsync();
                    var expiring = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw);
                    foreach (var m in expiring.EnumerateArray())
                    {
                        var endDate = DateTime.Parse(m.GetProperty("endDate").GetString()!);
                        var daysLeft = (int)Math.Ceiling((endDate - DateTime.UtcNow).TotalDays);
                        items.Add(new
                        {
                            itemId = $"mandate-{m.GetProperty("soleMandateId").GetInt32()}",
                            category = "compliance.expiring_mandate",
                            severity = daysLeft <= 0 ? "critical" : daysLeft <= 7 ? "high" : "warning",
                            title = daysLeft <= 0
                                ? $"Mandate EXPIRED on property {m.GetProperty("propertyId").GetInt32()}"
                                : $"Sole mandate expires in {daysLeft} day{(daysLeft == 1 ? "" : "s")} (property {m.GetProperty("propertyId").GetInt32()})",
                            body = $"End date: {endDate:yyyy-MM-dd}",
                            linkType = "property",
                            linkId = m.GetProperty("propertyId").GetInt32().ToString(),
                            icon = daysLeft <= 0 ? "🚨" : "⚠️",
                            daysLeft
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "GetComplianceInbox: could not fetch expiring mandates from property service");
            }

            return Ok(items.OrderBy(i => ((dynamic)i).severity == "critical" ? 0 : 1).ToList());
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] string? tenantId, [FromQuery] bool unreadOnly = false, [FromQuery] int limit = 50)
        {
            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            var tenantIds = await GetVisibleTenantIdsAsync(tenantId);
            if (tenantIds.Count == 0) return Ok(Array.Empty<object>());

             var q = NotificationRepository.Query()
                 .Where(n =>
                     // User-targeted notifications follow the user across tenants:
                     // an admin acting from a different (or "all-tenants") context
                     // can still reach the broker, who reads them from their own
                     // tenant view.
                     n.UserId == callerId
                     // Tenant-broadcast notifications (no recipient) are scoped
                     // to whatever the caller can see.
                     || (string.IsNullOrEmpty(n.UserId) && tenantIds.Contains(n.TenantId))
                 );

            if (unreadOnly) q = q.Where(n => !n.IsRead);

            var items = await q
                .OrderByDescending(n => n.CreatedAt)
                .Take(Math.Clamp(limit, 1, 200))
                .Select(n => new
                {
                    n.NotificationId,
                    n.Category,
                    n.Title,
                    n.Body,
                    n.LinkType,
                    n.LinkId,
                    n.IsRead,
                    n.CreatedAt
                })
                .ToListAsync();

            var unreadCount = await NotificationRepository.Query()
                .CountAsync(n => (n.UserId == callerId || (string.IsNullOrEmpty(n.UserId) && tenantIds.Contains(n.TenantId))) && !n.IsRead);

            return Ok(new { items, unreadCount });
        }

        // ✏️ Mark one notification as read.
        [Authorize]
        [HttpPost("notifications/{notificationId:int}/read")]
        public async Task<IActionResult> MarkNotificationRead(int notificationId)
        {
            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            var tenantIds = await GetVisibleTenantIdsAsync(null);
             var n = await NotificationRepository.Query().FirstOrDefaultAsync(x => x.NotificationId == notificationId
                 && (x.UserId == callerId || tenantIds.Contains(x.TenantId)));
            if (n == null) return NotFound(new { message = "Notification not found." });
            n.IsRead = true;
            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Marked as read." });
        }

        // ➕ Remind agent: admin clicks "Remind Agent" on a compliance item
        // → create a notification for the broker (or all brokers on the deal).
        [Authorize]
        [HttpPost("notifications")]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationPayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:edit"))
                return Forbid();
             if (payload == null) return BadRequest(new { message = "Payload is required." });
             var tenantIds = await GetVisibleTenantIdsAsync(null);
            // The notification lives in the admin's tenant — first one.
            var tenantId = tenantIds.FirstOrDefault();
            if (string.IsNullOrEmpty(tenantId))
                return BadRequest(new { message = "No visible tenant for the caller." });

            var n = new Notification
            {
                TenantId = tenantId,
                UserId = payload.UserId,
                Category = string.IsNullOrWhiteSpace(payload.Category) ? "reminder" : payload.Category,
                Title = payload.Title,
                Body = payload.Body ?? string.Empty,
                LinkType = payload.LinkType ?? string.Empty,
                LinkId = payload.LinkId ?? string.Empty,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            NotificationRepository.Add(n);
            await DbContext.SaveChangesAsync();
            return Created($"/crm/notifications/{n.NotificationId}", n);
        }

        // 📢 Agency-wide messaging: admin broadcasts a message to all users
        // in the current tenant, optionally filtered by role.
        [Authorize]
        [HttpPost("agency/message")]
        public async Task<IActionResult> BroadcastAgencyMessage([FromBody] BroadcastMessagePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
                return Forbid();

            if (payload == null || string.IsNullOrWhiteSpace(payload.Title))
                return BadRequest(new { message = "Title is required." });

            var tenantIds = await GetVisibleTenantIdsAsync(null);
            if (tenantIds.Count == 0)
                return BadRequest(new { message = "No visible tenant for the caller." });

            var role = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            var tenantId = tenantIds.FirstOrDefault();

            // Resolve target role into notification category
            var category = string.IsNullOrWhiteSpace(payload.RoleFilter)
                ? "announcement"
                : $"agency:{payload.RoleFilter.ToLower()}";

            // Create a tenant-broadcast notification (UserId = null → visible to all in tenant)
            var notification = new Notification
            {
                TenantId = tenantId,
                UserId = string.Empty, // broadcast to everyone in the tenant
                Category = category,
                Title = payload.Title,
                Body = payload.Body ?? string.Empty,
                LinkType = payload.LinkType ?? string.Empty,
                LinkId = payload.LinkId ?? string.Empty,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            NotificationRepository.Add(notification);
            await DbContext.SaveChangesAsync();

            Logger.LogInformation("BroadcastAgencyMessage: caller={Caller} tenant={Tenant} roleFilter={RoleFilter} title={Title}",
                callerId, tenantId, payload.RoleFilter ?? "(all)", payload.Title);

            return Created($"/crm/notifications/{notification.NotificationId}", new
            {
                message = "Agency message broadcast.",
                notificationId = notification.NotificationId,
                recipients = "all_users_in_tenant",
                roleFilter = payload.RoleFilter ?? "(all)"
            });
        }

    }
}
