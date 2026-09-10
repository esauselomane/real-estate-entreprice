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
        INotificationRepository notificationRepository)
        : base(dbContext, http, configuration, logger, permissionCheck,
               agencyRepository, brokerRepository, leadRepository, dealRepository,
               companyRepository, legalEntityRepository, contactRepository,
               geographyRepository, operationalRepository, brochureCardRepository,
               brochureCardLineRepository, dealBrokerSplitRepository, notificationRepository)
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
}
