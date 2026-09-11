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
/// Handles legal entity management: legal entity listing, creation,
/// update, search, and single-entity retrieval.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm")]
public class LegalEntitiesController : CRMControllerBase
{
    public LegalEntitiesController(
        CRMDbContext dbContext,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<LegalEntitiesController> logger,
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

        [HttpGet("legal-entities")]
        public async Task<IActionResult> GetLegalEntities([FromQuery] int page, [FromQuery] string? tenantId)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("GetLegalEntities: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMLegalEntity> query = LegalEntityRepository.Query().AsNoTracking().Include(e => e.CompanyDetails);
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
            if (!await PermissionCheck.HasPermissionAsync("agencies:create"))
            {
                Logger.LogWarning("CreateLegalEntity: forbidden — caller lacks agencies:create permission");
                return Forbid();
            }

            if (string.IsNullOrEmpty(legalEntity.LegalEntityId))
                legalEntity.LegalEntityId = "LGE-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
            // 🔐 Tenant taken from the caller's JWT, never from the request body.
            legalEntity.TenantId = GetCurrentTenant();
            legalEntity.CreatedAt = DateTime.UtcNow;
            LegalEntityRepository.Add(legalEntity);
            await DbContext.SaveChangesAsync();
            return Ok(legalEntity);
        }
        [HttpPut("legal-entity/update")]
        public async Task<IActionResult> UpdateLegalEntity([FromBody] CRMLegalEntity updatedEntity)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:edit"))
            {
                Logger.LogWarning("UpdateLegalEntity: forbidden — caller lacks agencies:edit permission");
                return Forbid();
            }

            var existing = await LegalEntityRepository.GetByIdAsync(updatedEntity.LegalEntityId);
            if (existing == null) return NotFound(new { message = "Legal Entity record not found." });

            existing.RegisteredLegalName = updatedEntity.RegisteredLegalName;
            existing.AssociatedCompanyId = updatedEntity.AssociatedCompanyId;
            existing.EntityTypeClassifier = updatedEntity.EntityTypeClassifier;
            existing.TaxRegistrationIndex = updatedEntity.TaxRegistrationIndex;
            existing.DeedsOfficeIdentifier = updatedEntity.DeedsOfficeIdentifier;

            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Legal Entity parameters updated successfully.", data = existing });
        }
        // 🔍 Auto-complete search over legal entities (by registered name / tax ref / type).
        [HttpGet("legal-entities/search")]
        public async Task<IActionResult> SearchLegalEntities([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("SearchLegalEntities: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            IQueryable<CRMLegalEntity> query = LegalEntityRepository.Query().AsNoTracking().Include(e => e.CompanyDetails);
            if (visibleTenants.Count > 0)
                query = query.Where(e => visibleTenants.Contains(e.TenantId));

            var term = (q ?? string.Empty).Trim().ToLower();
            if (!string.IsNullOrEmpty(term))
            {
                query = query.Where(e =>
                    e.RegisteredLegalName.ToLower().Contains(term) ||
                    e.EntityTypeClassifier.ToLower().Contains(term) ||
                    e.TaxRegistrationIndex.ToLower().Contains(term));
            }

            var (items, total) = await PageAsync(query.OrderBy(e => e.RegisteredLegalName), page);
            HttpContext.Response.Headers["X-Total-Count"] = total.ToString();
            return Ok(items);
        }
        // --- 📋 ENDPOINT: FETCH A SINGLE LEGAL ENTITY BY ID (edit hydration) ---
        [HttpGet("legal-entities/{legalEntityId}")]
        public async Task<IActionResult> GetLegalEntityById([FromRoute] string legalEntityId, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("agencies:view"))
            {
                Logger.LogWarning("GetLegalEntityById: forbidden — caller lacks agencies:view permission");
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(legalEntityId))
            {
                return BadRequest(new { message = "Legal entity id is required." });
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var entity = await LegalEntityRepository.Query()
                .AsNoTracking()
                .Include(e => e.CompanyDetails)
                .FirstOrDefaultAsync(e => e.LegalEntityId == legalEntityId);
            if (entity == null) return NotFound(new { message = "Legal entity record not found." });

            if (visibleTenants.Count > 0 && !visibleTenants.Contains(entity.TenantId))
                return Forbid();

            return Ok(entity);
        }
}
