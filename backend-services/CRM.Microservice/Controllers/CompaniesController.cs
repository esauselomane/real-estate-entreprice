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

namespace CRM.Microservice.Controllers;
/// <summary>
/// Handles company management: company listing, creation, update,
/// search, and single-company retrieval.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm")]
public class CompaniesController : CRMControllerBase
{
    public CompaniesController(
        CRMDbContext dbContext,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<CompaniesController> logger,
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

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("GetCompanies: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMCompany> query = CompanyRepository.Query();
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
        [HttpPost("company/create")]
        public async Task<IActionResult> CreateCompany([FromBody] CRMCompany company)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:create"))
            {
                Logger.LogWarning("CreateCompany: forbidden — caller lacks agencies:create permission");
                return Forbid();
            }

            if (string.IsNullOrEmpty(company.CompanyId)) company.CompanyId = "COM-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            company.TenantId = GetCurrentTenant();
            company.CreatedAt = DateTime.UtcNow;

            // 🔒 If a legal entity is assigned, it must exist and sit in the
            // caller's visible tenant subtree.
            if (!string.IsNullOrEmpty(company.AssociatedLegalEntityId))
            {
                var visibleTenants = await GetVisibleTenantIdsAsync();
                var entity = await LegalEntityRepository.Query()
                    .FirstOrDefaultAsync(e => e.LegalEntityId == company.AssociatedLegalEntityId);
                if (entity == null) return BadRequest(new { message = "Referenced legal entity not found." });
                if (!visibleTenants.Contains(entity.TenantId)) return Forbid();
            }

            CompanyRepository.Add(company);
            await DbContext.SaveChangesAsync();
            return Ok(company);
        }
        [HttpPut("company/update")]
        public async Task<IActionResult> UpdateCompany([FromBody] CRMCompany updatedCompany)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:edit"))
            {
                Logger.LogWarning("UpdateCompany: forbidden — caller lacks agencies:edit permission");
                return Forbid();
            }

            var permittedTenantIds = await GetVisibleTenantIdsAsync(null);
            var existing = await CompanyRepository.Query().FirstOrDefaultAsync(company => company.CompanyId == updatedCompany.CompanyId && permittedTenantIds.Contains(company.TenantId));
            if (existing == null) return NotFound(new { message = "Company record not found." });

            existing.BusinessName = updatedCompany.BusinessName;
            existing.RegistrationNumber = updatedCompany.RegistrationNumber;
            existing.VatNumber = updatedCompany.VatNumber;
            existing.CorporateWebsite = updatedCompany.CorporateWebsite;
            existing.HeadOfficeAddress = updatedCompany.HeadOfficeAddress;
            existing.CorporateCreditLimit = updatedCompany.CorporateCreditLimit;
            existing.IndustryVertical = updatedCompany.IndustryVertical;
            existing.AssociatedLegalEntityId = updatedCompany.AssociatedLegalEntityId;

            // 🔒 If a legal entity is assigned, it must exist and sit inside the
            // caller's visible tenant subtree (defence-in-depth — the permission
            // gate above is the primary control).
            if (!string.IsNullOrEmpty(existing.AssociatedLegalEntityId))
            {
                var visibleTenants = await GetVisibleTenantIdsAsync();
                var entity = await LegalEntityRepository.Query()
                    .FirstOrDefaultAsync(e => e.LegalEntityId == existing.AssociatedLegalEntityId);
                if (entity == null) return BadRequest(new { message = "Referenced legal entity not found." });
                if (!visibleTenants.Contains(entity.TenantId))
                    return Forbid();
            }

            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Company parameters updated successfully.", data = existing });
        }
        // 🔍 Auto-complete search over companies (by name / registration / VAT).
        [HttpGet("companies/search")]
        public async Task<IActionResult> SearchCompanies([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("SearchCompanies: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMCompany> query = CompanyRepository.Query();
                query = query.Where(c => visibleTenants.Contains(c.TenantId));

            var term = (q ?? string.Empty).Trim().ToLower();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(c =>
                    c.BusinessName.ToLower().Contains(term) ||
                    c.RegistrationNumber.ToLower().Contains(term) ||
                    c.VatNumber.ToLower().Contains(term));
            }

            var (items, total) = await PageAsync(query.OrderBy(c => c.BusinessName), page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }
        // --- 📋 ENDPOINT: FETCH A SINGLE COMPANY BY ID (edit hydration) ---
        [HttpGet("companies/{companyId}")]
        public async Task<IActionResult> GetCompanyById([FromRoute] string companyId, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("GetCompanyById: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(companyId))
            {
                return BadRequest(new { message = "Company id is required." });
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var company = await CompanyRepository.Query()
                .AsNoTracking()
                .Include(c => c.LegalEntityDetails)
                .FirstOrDefaultAsync(c => c.CompanyId == companyId);
            if (company == null) return NotFound(new { message = "Company record not found." });

            if (!visibleTenants.Contains(company.TenantId))
                return Forbid();

            return Ok(company);
        }
}
