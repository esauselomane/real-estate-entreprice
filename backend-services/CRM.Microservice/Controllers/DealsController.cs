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
/// Handles deal management: deal CRUD, deal notes, reallocation,
/// deal stage transitions, broker splits, and contact-to-deal linking.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm")]
public class DealsController : CRMControllerBase
{
    [HttpGet("deals/{dealId:int}/presentation")]
    public async Task<IActionResult> GetDealPresentation(int dealId)
    {
        if (!await PermissionCheck.HasPermissionAsync("billing:view")) return Forbid();
        var tenants = await GetVisibleTenantIdsAsync(null);
        var deal = await DealRepository.Query().SingleOrDefaultAsync(deal => deal.DealId == dealId && tenants.Contains(deal.TenantId));
        if (deal == null) return NotFound();
        if (GetCurrentCanonicalRole() == "Broker" && !CallerIsAssignedTo(deal.AllocatedBrokerIds, GetCurrentUserId())) return Forbid();
        return Ok(await Services.Brochures.DealPresentationHelper.ResolveAsync(deal, BrokerRepository.Query(), AgencyRepository.Query()));
    }

    public DealsController(
        CRMDbContext dbContext,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<DealsController> logger,
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

        // ❌ Delete a deal — restricted to AgencyAdmin and above (PlatformAdmin).
        [HttpDelete("deals/{dealId:int}")]
        public async Task<IActionResult> DeleteDeal(int dealId)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:delete"))
            {
                Logger.LogWarning("DeleteDeal: forbidden — caller lacks deals:delete permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            if (callerRole != "AgencyAdmin" && callerRole != "PlatformAdmin")
                return Forbid();

            var deal = await DealRepository.Query()
                .FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == GetCurrentTenant());
            if (deal == null) return NotFound(new { message = "Target deal record not found." });

            DealRepository.Remove(deal);
            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Deal record deleted." });
        }
        [HttpPut("deal/{dealId:int}")]
        public async Task<IActionResult> UpdateDeal(int dealId, [FromBody] UpdateDealPayload updatedDeal)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:edit"))
            {
                Logger.LogWarning("UpdateDeal: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            // 🛡️ PlatformAdmin can edit any deal; everyone else is pinned to
            // their JWT tenant to prevent cross-tenant tampering.
            var callerRole = GetCurrentCanonicalRole();
            var isPlatformAdmin = callerRole == "PlatformAdmin";
            var tenantId = GetCurrentTenant();
            var deal = isPlatformAdmin
                ? await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId)
                : await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
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

            await DbContext.SaveChangesAsync();
            return Ok(deal);
        }
        // 📝 Append a timestamped note to a deal's DealNotes. Used by the
        // broker dashboard "Review" action so the broker can log an intervention
        // (e.g. "Reviewed — awaiting broker signature") directly into the deal
        // audit trail without first fetching the existing notes.
        [HttpPost("deal/{dealId:int}/notes")]
        public async Task<IActionResult> AppendDealNote(int dealId, [FromBody] DealNotePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:edit"))
            {
                Logger.LogWarning("AppendDealNote: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            var callerId = GetCurrentUserId();
            var isPlatformAdmin = callerRole == "PlatformAdmin";
            var tenantId = GetCurrentTenant();
            var deal = isPlatformAdmin
                ? await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId)
                : await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            if (deal.DealStage == DealWorkflowStage.ClosedWon || deal.DealStage == DealWorkflowStage.ClosedLost)
                return BadRequest(new { message = "This deal is closed and cannot be edited." });

            var entry = $"[Reviewed by {callerId} on {DateTime.UtcNow:yyyy-MM-dd HH:mm 'UTC'}]: {payload?.NoteText ?? string.Empty}".TrimEnd();
            deal.DealNotes = string.IsNullOrWhiteSpace(deal.DealNotes) ? entry : deal.DealNotes + "\n" + entry;
            deal.LastStageUpdatedAt = DateTime.UtcNow;

            await DbContext.SaveChangesAsync();
            return Ok(deal);
        }
        [HttpPost("reallocate")]
        public async Task<IActionResult> ReallocateTarget([FromBody] ReallocatePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:edit"))
            {
                Logger.LogWarning("ReallocateTarget: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            if (payload.IsDeal)
            {
                var deal = await DealRepository.GetByIdAsync(payload.TargetId);
                if (deal == null) return NotFound(new { message = "Target deal record not found." });
                // payload.TargetAgentName is actually a broker id now (legacy field name kept
                // for frontend compat — same payload shape, just an id instead of a display name).
                deal.AllocatedBrokerIds = payload.TargetAgentName;
            }
            else
            {
                var lead = await LeadRepository.GetByIdAsync(payload.TargetId);
                if (lead == null) return NotFound(new { message = "Target lead record not found." });
                lead.AllocatedBrokerIds = payload.TargetAgentName;
            }

            await DbContext.SaveChangesAsync();
            return Ok(new { message = $"Assignment safely reallocated to {payload.TargetAgentName}." });
        }
        [HttpPost("update-deal-stage")]
        public async Task<IActionResult> UpdateDealStage([FromBody] DealUpdatePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:edit"))
            {
                Logger.LogWarning("UpdateDealStage: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var deal = await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == payload.DealId && d.TenantId == tenantId);
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

            await DbContext.SaveChangesAsync();
            return Ok(new { message = $"Deal successfully progressed to {payload.NextStage}." });
        }
        // 💰 Co-brokerage split management (AgencyAdmin only).
        [HttpGet("deals/{dealId:int}/broker-splits")]
        public async Task<IActionResult> GetDealBrokerSplits(int dealId)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetDealBrokerSplits: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var deal = await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            var splits = await DealBrokerSplitRepository.Query()
                .Where(s => s.DealId == dealId)
                .OrderByDescending(s => s.SplitPercentage)
                .ToListAsync();
            return Ok(splits);
        }
        [HttpPut("deals/{dealId:int}/broker-splits")]
        public async Task<IActionResult> UpsertDealBrokerSplits(int dealId, [FromBody] UpsertBrokerSplitsRequest request)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:edit"))
            {
                Logger.LogWarning("UpsertDealBrokerSplits: forbidden — caller lacks billing:edit permission");
                return Forbid();
            }

            var callerRole = GetCurrentCanonicalRole();
            if (callerRole != "AgencyAdmin" && callerRole != "PlatformAdmin")
                return Forbid();

            var tenantId = GetCurrentTenant();
            var deal = await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal record not found." });

            // 🔒 Closed deals: splits are locked because the realised
            // commission has already been distributed.
            if (deal.DealStage == DealWorkflowStage.ClosedWon || deal.DealStage == DealWorkflowStage.ClosedLost)
            {
                return BadRequest(new { message = "This deal is closed and commission splits cannot be edited." });
            }

            if (request.Splits == null || !request.Splits.Any())
            {
                DealBrokerSplitRepository.RemoveRange(DealBrokerSplitRepository.Query().Where(s => s.DealId == dealId));
                await DbContext.SaveChangesAsync();
                return Ok(new { message = "All broker splits cleared." });
            }

            var requestedIds = request.Splits.Select(split => split.BrokerId).ToArray();
            var allocatedIds = Services.Brochures.DealPresentationHelper.BrokerIds(deal.AllocatedBrokerIds);
            if (requestedIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != requestedIds.Length ||
                request.Splits.Any(split => split.SplitPercentage < 0 || split.SplitPercentage > 100) ||
                request.Splits.Sum(split => split.SplitPercentage) > 100 ||
                requestedIds.Any(id => !allocatedIds.Contains(id, StringComparer.OrdinalIgnoreCase)))
                return BadRequest(new { message = "Splits must use distinct assigned brokers, non-negative percentages, and total no more than 100%." });

            var existing = DealBrokerSplitRepository.Query().Where(s => s.DealId == dealId);
            DealBrokerSplitRepository.RemoveRange(existing);

            decimal totalPercentage = request.Splits.Sum(s => s.SplitPercentage);
            // 🧮 Split the realised commission, not the raw CommissionValue
            // (CommissionValue is either a percentage like 5 or a flat Rand
            // amount — both need to be normalised to Rand before splitting).
            decimal realisedCommission = deal.CommissionType == CommissionTypes.Percent
                ? Math.Round(deal.TransactionValue * deal.CommissionValue / 100m, 2)
                : deal.CommissionValue;

            foreach (var split in request.Splits)
            {
                DealBrokerSplitRepository.Add(new DealBrokerSplit
                {
                    DealId = dealId,
                    BrokerId = split.BrokerId,
                    BrokerName = split.BrokerName,
                    SplitPercentage = split.SplitPercentage,
                    SplitAmount = totalPercentage > 0 ? Math.Round(realisedCommission * split.SplitPercentage / 100m, 2) : 0m
                });
            }

            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Broker splits updated.", totalPercentage });
        }
        // 🔗 Link a contact to a deal (adds DealId to contact's LinkedDealIds CSV).
        [HttpPost("contacts/{contactId}/link-deal/{dealId:int}")]
        public async Task<IActionResult> LinkContactToDeal(string contactId, int dealId)
        {
            if (!await PermissionCheck.HasPermissionAsync("deals:edit"))
            {
                Logger.LogWarning("LinkContactToDeal: forbidden — caller lacks deals:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var contact = await ContactRepository.Query().FirstOrDefaultAsync(c => c.ContactId == contactId && c.TenantId == tenantId);
            if (contact == null) return NotFound(new { message = "Contact not found." });

            var deal = await DealRepository.Query().FirstOrDefaultAsync(d => d.DealId == dealId && d.TenantId == tenantId);
            if (deal == null) return NotFound(new { message = "Deal not found." });

            var linked = string.IsNullOrWhiteSpace(contact.LinkedDealIds)
                ? new List<string>()
                : contact.LinkedDealIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

            if (!linked.Contains(dealId.ToString()))
                linked.Add(dealId.ToString());

            contact.LinkedDealIds = string.Join(',', linked);
            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Contact linked to deal.", linkedDealIds = contact.LinkedDealIds });
        }

        // 🧾 brochure cards — retrieved by dealId (broker dashboard card viewer).
        [HttpGet("brochure-cards")]
        public async Task<IActionResult> GetBrochureCardByDeal([FromQuery] int dealId, [FromQuery] string? tenantId = null)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetBrochureCardByDeal: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";

            IQueryable<BrochureCard> cardQuery = BrochureCardRepository.Query()
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
            if (!await PermissionCheck.HasPermissionAsync("billing:view"))
            {
                Logger.LogWarning("GetActiveBrochureCards: forbidden — caller lacks billing:view permission");
                return Forbid();
            }

            var visibleTenants = await GetVisibleTenantIdsAsync(tenantId);
            var callerName = User?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty;
            var isBroker = GetCurrentCanonicalRole() == "Broker";

            IQueryable<BrochureCard> query = BrochureCardRepository.Query()
                .Where(c => !c.IsDeleted &&
                            (c.Status == BrochureCardStatus.Active || c.Status == BrochureCardStatus.Generating));
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
            if (!await PermissionCheck.HasPermissionAsync("billing:edit"))
            {
                Logger.LogWarning("UpsertBrochureCard: forbidden — caller lacks billing:edit permission");
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
                var deal = await DealRepository.Query()
                    .FirstOrDefaultAsync(d => d.DealId == request.DealId && d.TenantId == tenantId);
                if (deal == null) return NotFound(new { message = "Target deal record not found." });
                if (!CallerIsAssignedTo(deal.AllocatedBrokerIds, brokerId)) return Forbid();
            }
            else
            {
                var dealExists = await DealRepository.Query().AnyAsync(d => d.DealId == request.DealId && d.TenantId == tenantId);
                if (!dealExists) return NotFound(new { message = "Target deal record not found." });
            }

            BrochureCard card;
            if (request.CardId.HasValue && request.CardId > 0)
            {
                card = await BrochureCardRepository.Query()
                    .Include(c => c.Lines)
                    .FirstOrDefaultAsync(c => c.CardId == request.CardId.Value && c.TenantId == tenantId);
                if (card == null) return NotFound(new { message = "Brochure card not found." });
                foreach (var ln in card.Lines.Where(l => !l.IsDeleted)) ln.IsDeleted = true;
            }
            else
            {
                card = new BrochureCard { TenantId = tenantId, DealId = request.DealId, BrokerAgentId = brokerName, Status = BrochureCardStatus.Active };
                BrochureCardRepository.Add(card);
                await DbContext.SaveChangesAsync();
            }

            card.ClientName = request.ClientName;
            card.ClientEmail = request.ClientEmail;
            card.ClientPhone = request.ClientPhone;
            card.CompanyName = request.CompanyName;
            card.CompanyLogoUrl = request.CompanyLogoUrl;
            card.UpdatedAt = DateTime.UtcNow;

            foreach (var unit in request.Units.Take(5))
            {
                BrochureCardLineRepository.Add(new BrochureCardLine
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

            await DbContext.SaveChangesAsync();
            return Ok(new { cardId = card.CardId });
        }

        // 🧾 Remove a single unit line from a broker's card.
        [HttpDelete("brochure-cards/{cardId:int}/lines/{lineId:int}")]
        public async Task<IActionResult> DeleteBrochureCardLine(int cardId, int lineId)
        {
            if (!await PermissionCheck.HasPermissionAsync("billing:delete"))
            {
                Logger.LogWarning("DeleteBrochureCardLine: forbidden — caller lacks billing:delete permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var line = await BrochureCardLineRepository.Query().FirstOrDefaultAsync(l => l.LineId == lineId && l.CardId == cardId);
            if (line == null) return NotFound(new { message = "Brochure card line not found." });
            var owned = await BrochureCardRepository.Query().AnyAsync(c => c.CardId == cardId && c.TenantId == tenantId);
            if (!owned) return Forbid();
            line.IsDeleted = true;
            await DbContext.SaveChangesAsync();
            return Ok(new { message = "Brochure card line removed." });
        }

        // 🧾 Worker callback (AllowAnonymous — the worker holds no bearer token):
        // close the card + advance the deal once the PDF has been rendered to disk.
        [AllowAnonymous]
        [HttpPost("brochure-cards/{cardId:int}/generated")]
        public async Task<IActionResult> CompleteBrochureCard(int cardId, [FromBody] BrochureCardGeneratedDto payload)
        {
            if (payload == null) return BadRequest(new { message = "Callback payload is required." });

            var card = await BrochureCardRepository.Query()
                .Include(c => c.Lines)
                .FirstOrDefaultAsync(c => c.CardId == cardId);
            if (card == null) return NotFound(new { message = "Brochure card not found." });

            card.Status = BrochureCardStatus.Generated;
            card.IsDeleted = true;
            card.GeneratedFilePath = string.IsNullOrWhiteSpace(payload.FilePath) ? card.GeneratedFilePath : payload.FilePath;
            card.UpdatedAt = DateTime.UtcNow;

            var deal = await DealRepository.Query()
                .FirstOrDefaultAsync(d => d.DealId == card.DealId && d.TenantId == card.TenantId);
            if (deal != null)
            {
                deal.DealStage = DealWorkflowStage.ProposalQuoteSent;
                deal.LastStageUpdatedAt = DateTime.UtcNow;
            }

            await DbContext.SaveChangesAsync();
            return Ok(new
            {
                message = "Brochure card closed and deal advanced to Proposal / Quote Sent.",
                cardId = card.CardId,
                dealStage = deal == null ? null : deal.DealStage.ToString()
            });
        }
    }
