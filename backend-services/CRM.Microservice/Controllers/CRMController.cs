using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Text.Json;
using CRM.Microservice.Data;
using CRM.Microservice.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public CRMController(CRMDbContext context, IHttpClientFactory http, IConfiguration configuration, ILogger<CRMController> logger)
        {
            _context = context;
            _http = http;
            _gatewayUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5000/gateway";
            _logger = logger;
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

                                                                                                                                                                                                                                                                                                                                                                                                                            
                                                                                                                                                                                        
                                                                                                                                                                         

        // 🚀 Promotion request: converts a qualified lead into a deal, auto-matching a property
        // from the Property microservice when one is not supplied manually.
                                     
         
                                                     
                                                             
                                                              
                                             
                                             
                                                      
                                                            
         

        // 🏠 Shape of a property returned by the Property microservice search (auto-match engine).
                                     
         
                                               
                                                      
                                                       
         


        [HttpGet("operations")]
        public async Task<IActionResult> GetOperationalRecords([FromQuery] int page = 1, [FromQuery] string? module = null, [FromQuery] int? propertyId = null, [FromQuery] int? propertyUnitId = null)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.OperationalRecords.Where(r => r.TenantId == tenantId);
            if (!string.IsNullOrWhiteSpace(module)) query = query.Where(r => r.Module == module);
            if (propertyId.HasValue) query = query.Where(r => r.PropertyId == propertyId.Value);
            if (propertyUnitId.HasValue) query = query.Where(r => r.PropertyUnitId == propertyUnitId.Value);

            var (items, total) = await PageAsync(query.OrderByDescending(r => r.UpdatedAt), page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpPost("operations")]
        public async Task<IActionResult> CreateOperationalRecord([FromBody] OperationalRecord record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.Module) || string.IsNullOrWhiteSpace(record.Title))
                return BadRequest(new { message = "Module and title are required." });
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
        public async Task<IActionResult> GetCompanies([FromQuery] int page)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.CRMCompanies.Where(c => c.TenantId == tenantId).OrderBy(c => c.BusinessName);
            var (items, total) = await PageAsync(query, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpGet("owners-tenants")]
        public async Task<IActionResult> GetOwnerTenantRecords([FromQuery] int page)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.OwnerTenantRecords.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.CreatedAt);
            var (items, total) = await PageAsync(query, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpGet("owners-tenants/{ownerTenantId:int}")]
        public async Task<IActionResult> GetOwnerTenantRecord(int ownerTenantId)
        {
            var record = await _context.OwnerTenantRecords.FindAsync(ownerTenantId);
            return record == null
                ? NotFound(new { message = "Owner or tenant record not found." })
                : Ok(record);
        }

        // 🌍 Geography auto-complete reference data for owner/tenant area-of-interest selection.
        [HttpGet("geography")]
        public async Task<IActionResult> SearchGeography([FromQuery] string? search)
        {
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
            var geo = await _context.GeographyRecords.FindAsync(geographyId);
            return geo == null ? NotFound(new { message = "Geography record not found." }) : Ok(geo);
        }

        [HttpPost("owners-tenants")]
        public async Task<IActionResult> CreateOwnerTenantRecord([FromBody] OwnerTenantRecord record)
        {
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
        public async Task<IActionResult> GetAgencies([FromQuery] int page)
        {
            var tenant = GetCurrentTenant();
            var callerRole = GetCurrentCanonicalRole();
            _logger.LogInformation("GetAgencies: Page={Page} Tenant={Tenant} Role={Role}", page, tenant, callerRole);

            var query = _context.AgencyRecords
                .Where(a => string.IsNullOrWhiteSpace(tenant) || a.TenantId == tenant)
                .OrderBy(a => a.Name);
            var (items, total) = await PageAsync(query, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            _logger.LogInformation("GetAgencies: Returned {Count} agencies (page {Page}) out of {Total} total for tenant {Tenant}",
                items.Count, page, total, tenant);
            return Ok(items);
        }

        [HttpPost("agencies")]
        public async Task<IActionResult> CreateAgency([FromBody] AgencyRecord agency)
        {
            var caller = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Unknown";
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
            _context.AgencyRecords.Add(agency);
            await _context.SaveChangesAsync();

            _logger.LogInformation("CreateAgency: created AgencyId={AgencyId} Name={Name} Tenant={Tenant} By={Caller}",
                agency.AgencyId, agency.Name, agency.TenantId, caller);
            return Ok(agency);
        }

        [HttpPut("agencies/{agencyId}")]
        public async Task<IActionResult> UpdateAgency(string agencyId, [FromBody] AgencyRecord updatedAgency)
        {
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
            await _context.SaveChangesAsync();
            return Ok(agency);
        }

        // 📇 Contact-person sub-resource for an agency (editable; promotable to a broker).
        [HttpGet("agencies/{agencyId}/contacts")]
        public async Task<IActionResult> GetAgencyContacts(string agencyId)
        {
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
        public async Task<IActionResult> GetBrokers([FromQuery] int page)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.BrokerRecords
                .Where(b => b.TenantId == tenantId)
                .OrderBy(b => b.AgencyId).ThenBy(b => b.FullName);
            var (items, total) = await PageAsync(query, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpPost("brokers")]
        public async Task<IActionResult> CreateBroker([FromBody] BrokerRecord broker)
        {
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
        public async Task<IActionResult> GetLegalEntities([FromQuery] int page)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.CRMLegalEntities
                .Include(e => e.CompanyDetails)
                .Where(e => e.TenantId == tenantId)
                .OrderBy(e => e.RegisteredLegalName);
            var (items, total) = await PageAsync(query, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpPost("legal-entity/create")]
        public async Task<IActionResult> CreateLegalEntity([FromBody] CRMLegalEntity legalEntity)
        {
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
        public async Task<IActionResult> GetContacts([FromQuery] int page)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.CRMContacts
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails)
                .Where(c => c.TenantId == tenantId)
                .OrderBy(c => c.FullName);
            var (items, total) = await PageAsync(query, page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }

        [HttpGet("contacts/search")]
        public async Task<IActionResult> SearchContacts([FromQuery] string q, [FromQuery] int page = 1)
        {
            var tenantId = GetCurrentTenant();
            var query = _context.CRMContacts
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails)
                .Where(c => c.TenantId == tenantId);

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
            var existing = await _context.CRMContacts.FindAsync(updatedContact.ContactId);
            if (existing == null) return NotFound(new { message = "Contact tracking profile record not found." });

            existing.FullName = updatedContact.FullName;
            existing.EmailAddress = updatedContact.EmailAddress;
            existing.PhoneNumber = updatedContact.PhoneNumber;
            existing.PositionJobTitle = updatedContact.PositionJobTitle;
            existing.ContactRoleType = updatedContact.ContactRoleType;
            existing.AssociatedCompanyId = updatedContact.AssociatedCompanyId;
            existing.AssociatedLegalEntityId = updatedContact.AssociatedLegalEntityId;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Contact directory record updated successfully.", data = existing });
        }

        [HttpGet("boards")]
        public async Task<IActionResult> GetCRMBoards([FromQuery] int page, [FromQuery] string agentName = "", [FromQuery] string userRole = "")
        {
            var tenantId = GetCurrentTenant();
            // 🔐 Scoping is authoritative from the JWT, never from a client-supplied role string.
            var callerRole = GetCurrentCanonicalRole();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? agentName ?? string.Empty;

            var leadsQuery = _context.LeadRecords.Where(l => l.TenantId == tenantId && !l.IsConvertedToDeal);
            var dealsQuery = _context.DealRecords.Where(d => d.TenantId == tenantId);

            // Only a Broker (agent) is restricted to their own records; admins see the full tenant pipeline.
            if (callerRole == "Broker")
            {
                leadsQuery = leadsQuery.Where(l => l.AllocatedToAgent == callerName);
                dealsQuery = dealsQuery.Where(d => d.AllocatedToAgent == callerName);
            }

            var (activeLeads, leadTotal) = await PageAsync(leadsQuery.OrderByDescending(l => l.CreatedAt), page);
            var (activeDeals, dealTotal) = await PageAsync(dealsQuery.OrderByDescending(d => d.LastStageUpdatedAt), page);
            HttpContext.Response.Headers["X-Lead-Count"] = leadTotal.ToString();
            HttpContext.Response.Headers["X-Deal-Count"] = dealTotal.ToString();

            return Ok(new
            {
                unverifiedLeads = activeLeads,
                verifiedDeals = activeDeals
            });
        }

         [HttpPost("lead/create")]
         public async Task<IActionResult> CreateLead([FromBody] LeadRecord newLead)
         {
             if (newLead == null) return BadRequest(new { message = "Invalid lead structural parameters." });

            var tenantId = GetCurrentTenant();
            newLead.TenantId = tenantId;
            // 🔐 A Broker always creates leads allocated to themselves — the client value is ignored
            // so an agent can never claim another broker's pipeline.
            if (GetCurrentCanonicalRole() == "Broker")
                newLead.AllocatedToAgent = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? newLead.AllocatedToAgent;
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
            var tenantId = GetCurrentTenant();
            var lead = await _context.LeadRecords.FirstOrDefaultAsync(l => l.LeadId == leadId && l.TenantId == tenantId);
            if (lead == null) return NotFound(new { message = "Lead record not found." });

            if (GetCurrentCanonicalRole() == "Broker")
                updatedLead.AllocatedToAgent = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? lead.AllocatedToAgent;

            lead.ClientName = updatedLead.ClientName;
            lead.ContactEmail = updatedLead.ContactEmail;
            lead.ContactPhone = updatedLead.ContactPhone;
            lead.LinkedContactId = updatedLead.LinkedContactId;
            lead.AllocatedToAgent = updatedLead.AllocatedToAgent;
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
                AllocatedToAgent = lead.AllocatedToAgent,
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
        public async Task<IActionResult> UpdateDeal(int dealId, [FromBody] DealRecord updatedDeal)
        {
            var tenantId = GetCurrentTenant();
            var deal = await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            if (GetCurrentCanonicalRole() == "Broker")
                updatedDeal.AllocatedToAgent = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? deal.AllocatedToAgent;

            deal.BuyerClientId = updatedDeal.BuyerClientId;
            deal.SellerClientId = updatedDeal.SellerClientId;
            deal.DealStage = updatedDeal.DealStage;
            deal.TransactionValue = updatedDeal.TransactionValue;
            deal.DealNotes = updatedDeal.DealNotes;
            deal.AllocatedToAgent = updatedDeal.AllocatedToAgent;
            deal.CommissionType = updatedDeal.CommissionType;
            deal.CommissionValue = updatedDeal.CommissionValue;
            deal.RealisedCommission = updatedDeal.RealisedCommission;
            deal.LinkedPropertyId = updatedDeal.LinkedPropertyId;
            deal.LastStageUpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(deal);
        }


        [HttpPost("reallocate")]
        public async Task<IActionResult> ReallocateTarget([FromBody] ReallocatePayload payload)
        {
            if (payload.IsDeal)
            {
                var deal = await _context.DealRecords.FindAsync(payload.TargetId);
                if (deal == null) return NotFound(new { message = "Target deal record not found." });
                deal.AllocatedToAgent = payload.TargetAgentName;
            }
            else
            {
                var lead = await _context.LeadRecords.FindAsync(payload.TargetId);
                if (lead == null) return NotFound(new { message = "Target lead record not found." });
                lead.AllocatedToAgent = payload.TargetAgentName;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Assignment safely reallocated to {payload.TargetAgentName}." });
        }

        [HttpPost("update-lead-stage")]
        public async Task<IActionResult> UpdateLeadStage([FromBody] LeadUpdatePayload payload)
        {
            var tenantId = GetCurrentTenant();
            var lead = await _context.LeadRecords.FirstOrDefaultAsync(l => l.LeadId == payload.LeadId && l.TenantId == tenantId);
            if (lead == null) return NotFound(new { message = "Target lead tracking record not found." });

            // 🔐 A Broker may only advance leads allocated to themselves.
            if (GetCurrentCanonicalRole() == "Broker" && lead.AllocatedToAgent != (User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty))
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
                    AllocatedToAgent = lead.AllocatedToAgent,
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
            var tenantId = GetCurrentTenant();
            var deal = await _context.DealRecords.FirstOrDefaultAsync(d => d.DealId == payload.DealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Target deal record not found." });

            // 🔐 A Broker may only advance deals allocated to themselves.
            if (GetCurrentCanonicalRole() == "Broker" && deal.AllocatedToAgent != (User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty))
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
            if (nextDealStageEnum == DealWorkflowStage.ClosedWon)
            {
                deal.RealisedCommission = deal.CommissionType == CommissionTypes.Percent
                    ? Math.Round(deal.TransactionValue * deal.CommissionValue / 100m, 2)
                    : deal.CommissionValue;
            }
            else if (nextDealStageEnum == DealWorkflowStage.ClosedLost)
            {
                deal.RealisedCommission = 0m;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Deal successfully progressed to {payload.NextStage}." });
        }

        [HttpGet("report/summary")]
        public async Task<IActionResult> GetPipelineReportSummary()
        {
            var tenantId = GetCurrentTenant();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";

            var baseLeadQuery = _context.LeadRecords
                .Where(l => l.TenantId == tenantId && !l.IsConvertedToDeal)
                .Where(l => !isBroker || l.AllocatedToAgent == callerName);

            var baseDealQuery = _context.DealRecords
                .Where(d => d.TenantId == tenantId)
                .Where(d => !isBroker || d.AllocatedToAgent == callerName);

            var summaryReport = new PipelineSummaryReport
            {
                ActiveLeadsCount = await baseLeadQuery.Where(l => l.LeadStage == LeadWorkflowStage.Contacted).CountAsync(),
                ValuationsInProcessCount = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.DiscoveryNeedsAnalysis || d.DealStage == DealWorkflowStage.Presentation).CountAsync(),
                ConveyancingAtDeedsCount = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.ProposalQuoteSent || d.DealStage == DealWorkflowStage.Negotiation).CountAsync(),
                FinalizedClosedCount = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).CountAsync(),
                TotalPipelineEstimatedValue = await baseDealQuery.Where(d => d.DealStage == DealWorkflowStage.DiscoveryNeedsAnalysis || d.DealStage == DealWorkflowStage.Presentation || d.DealStage == DealWorkflowStage.ProposalQuoteSent || d.DealStage == DealWorkflowStage.Negotiation).SumAsync(d => d.TransactionValue)
            };

            _logger.LogInformation("GetPipelineReportSummary: tenant={Tenant} | ActiveLeads={Active} Valuations={Valuations} Conveyancing={Conveyancing} ClosedWon={ClosedWon} PipelineValue={PipelineValue}",
                tenantId, summaryReport.ActiveLeadsCount, summaryReport.ValuationsInProcessCount, summaryReport.ConveyancingAtDeedsCount, summaryReport.FinalizedClosedCount, summaryReport.TotalPipelineEstimatedValue);

            return Ok(summaryReport);
        }

        [HttpGet("report/overview")]
        public async Task<IActionResult> GetReportOverview([FromQuery] int? year)
        {
            var tenantId = GetCurrentTenant();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";
            var activeYear = year ?? DateTime.UtcNow.Year;
            _logger.LogInformation("GetReportOverview: Year={Year} Tenant={Tenant}", activeYear, tenantId);

            var yearStart = new DateTime(activeYear, 1, 1);
            var yearEnd = new DateTime(activeYear + 1, 1, 1);

            var tenantLeads = await _context.LeadRecords
                .Where(l => l.TenantId == tenantId && l.CreatedAt >= yearStart && l.CreatedAt < yearEnd)
                .Where(l => !isBroker || l.AllocatedToAgent == callerName)
                .ToListAsync();
            var tenantDeals = await _context.DealRecords
                .Where(d => d.TenantId == tenantId && d.CreatedAt >= yearStart && d.CreatedAt < yearEnd)
                .Where(d => !isBroker || d.AllocatedToAgent == callerName)
                .ToListAsync();

            var monthlyBreakdown = Enumerable.Range(1, 12)
                .Select(month => new ReportDrilldownRow
                {
                    Period = new DateTime(activeYear, month, 1).ToString("MMM"),
                    Leads = tenantLeads.Count(l => l.CreatedAt.Month == month),
                    Deals = tenantDeals.Count(d => d.CreatedAt.Month == month),
                    Revenue = tenantDeals.Where(d => d.CreatedAt.Month == month && d.DealStage == DealWorkflowStage.ClosedWon).Sum(d => d.TransactionValue)
                })
                .ToList();

            return Ok(new CRMReportOverview
            {
                TotalLeads = tenantLeads.Count,
                TotalDeals = tenantDeals.Count,
                TotalRevenue = tenantDeals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).Sum(d => d.TransactionValue),
                TotalRealisedCommission = tenantDeals.Where(d => d.DealStage == DealWorkflowStage.ClosedWon).Sum(d => d.RealisedCommission ?? 0m),
                MonthlyBreakdown = monthlyBreakdown
            });
        }

        // 📊 Commission realised per deal stage — only ClosedWon carries a realised amount.
        [HttpGet("report/commission-by-stage")]
        public async Task<IActionResult> GetCommissionByStage()
        {
            var tenantId = GetCurrentTenant();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";

            var query = _context.DealRecords
                .Where(d => d.TenantId == tenantId)
                .Where(d => !isBroker || d.AllocatedToAgent == callerName);

            var rows = await query
                .GroupBy(d => d.DealStage)
                .Select(g => new ReportDrilldownRow
                {
                    Period = g.Key.ToString(),
                    Deals = g.Count(),
                    Revenue = g.Sum(d => d.RealisedCommission ?? 0m)
                })
                .ToListAsync();
            return Ok(rows);
        }

        // 📊 Broker leaderboard — deal count + pipeline value, ranked.
        [HttpGet("report/broker-leaderboard")]
        public async Task<IActionResult> GetBrokerLeaderboard()
        {
            var tenantId = GetCurrentTenant();
            var rows = await _context.DealRecords
                .Where(d => d.TenantId == tenantId && d.AllocatedToAgent != null && d.AllocatedToAgent != "")
                .GroupBy(d => d.AllocatedToAgent)
                .Select(g => new
                {
                    agent = g.Key,
                    deals = g.Count(),
                    totalValue = g.Sum(d => d.TransactionValue),
                    totalCommission = g.Sum(d => d.RealisedCommission ?? 0m),
                    won = g.Count(d => d.DealStage == DealWorkflowStage.ClosedWon)
                })
                .OrderByDescending(r => r.totalValue)
                .ToListAsync();
            return Ok(rows);
        }

        // 🧾 Load a broker's resumable brochure card for a deal (client details + curated unit lines).
        [HttpGet("brochure-cards")]
        public async Task<IActionResult> GetBrochureCardByDeal([FromQuery] int dealId)
        {
            var tenantId = GetCurrentTenant();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";

            var card = await _context.BrochureCards
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.DealId == dealId && c.TenantId == tenantId && !c.IsDeleted);
            if (card == null) return NotFound(new { message = "No brochure card found for this deal." });
            if (isBroker && card.BrokerAgentId != callerName) return Forbid();

            return Ok(new
            {
                cardId = card.CardId,
                dealId = card.DealId,
                clientName = card.ClientName,
                clientEmail = card.ClientEmail,
                clientPhone = card.ClientPhone,
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
                        netRentableAreaSqM = l.NetRentableAreaSqM,
                        imageRef = l.ImageRef
                    })
            });
        }

        // 🧾 Active brochure cards for the broker notification badge.
        [HttpGet("brochure-cards/active")]
        public async Task<IActionResult> GetActiveBrochureCards()
        {
            var tenantId = GetCurrentTenant();
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";

            var query = _context.BrochureCards
                .Where(c => c.TenantId == tenantId && !c.IsDeleted &&
                            (c.Status == BrochureCardStatus.Active || c.Status == BrochureCardStatus.Generating));
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
            if (request == null || request.DealId <= 0)
                return BadRequest(new { message = "DealId is required for a brochure card." });

            var tenantId = GetCurrentTenant();
            var brokerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var role = GetCurrentCanonicalRole();

            if (role == "Broker")
            {
                var ownsDeal = await _context.DealRecords
                    .AnyAsync(d => d.DealId == request.DealId && d.TenantId == tenantId && d.AllocatedToAgent == brokerName);
                if (!ownsDeal) return Forbid();
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
                    NetRentableAreaSqM = unit.NetRentableAreaSqM,
                    ImageRef = unit.ImageRef ?? string.Empty
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { cardId = card.CardId });
        }

        // 🧾 Remove a single unit line from a broker's card.
        [HttpDelete("brochure-cards/{cardId:int}/lines/{lineId:int}")]
        public async Task<IActionResult> DeleteBrochureCardLine(int cardId, int lineId)
        {
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
    }
}
