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
/// Handles lead management: CRM boards, lead CRUD, lead-to-deal promotion,
/// and lead stage transitions.
/// </summary>
[ApiController]
[Authorize]
[Route("api/crm")]
public class LeadsController : CRMControllerBase
{
    public LeadsController(
        CRMDbContext dbContext,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<LeadsController> logger,
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

        [HttpPost("lead/create")]
         public async Task<IActionResult> CreateLead([FromBody] LeadRecord newLead)
         {
             if (!await PermissionCheck.HasPermissionAsync("leads:create"))
             {
                 Logger.LogWarning("CreateLead: forbidden — caller lacks leads:create permission");
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
                 ? await ContactRepository.Query().FirstOrDefaultAsync(c => c.TenantId == tenantId && c.EmailAddress == newLead.ContactEmail)
                 : null;
             if (match == null && !string.IsNullOrWhiteSpace(newLead.ClientName))
             {
                 var term = newLead.ClientName.ToLower();
                 var surname = (newLead.ClientName.Split(' ').LastOrDefault() ?? "");
                 match = await ContactRepository.Query()
                     .Where(c => c.TenantId == tenantId && c.FullName != null)
                     .FirstOrDefaultAsync(c => c.FullName.ToLower().Contains(term) ||
                                               (surname.Length > 2 && c.FullName.ToLower().Contains(surname)));
             }
             if (match != null) newLead.LinkedContactId = match.ContactId;

             LeadRepository.Add(newLead);
             await DbContext.SaveChangesAsync();

              return Ok(new { message = "Unverified lead record successfully catalogued.", leadId = newLead.LeadId, linkedContactId = newLead.LinkedContactId });
          }
        [HttpPut("lead/{leadId:int}")]
        public async Task<IActionResult> UpdateLead(int leadId, [FromBody] LeadRecord updatedLead)
        {
            if (!await PermissionCheck.HasPermissionAsync("leads:edit"))
            {
                Logger.LogWarning("UpdateLead: forbidden — caller lacks leads:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var lead = await LeadRepository.Query().FirstOrDefaultAsync(l => l.LeadId == leadId && l.TenantId == tenantId);
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

            await DbContext.SaveChangesAsync();
            return Ok(lead);
        }
        // 🚀 Promote a lead to a deal with automatic property matching.
        [HttpPost("leads/{leadId:int}/promote")]
        public async Task<IActionResult> PromoteLeadToDeal(int leadId, [FromBody] PromotionRequest request)
        {
            if (!await PermissionCheck.HasPermissionAsync("leads:edit"))
            {
                Logger.LogWarning("PromoteLeadToDeal: forbidden — caller lacks leads:edit permission");
                return Forbid();
            }

            var lead = await LeadRepository.Query()
                .FirstOrDefaultAsync(l => l.LeadId == leadId && l.TenantId == GetCurrentTenant());
            if (lead == null) return NotFound(new { message = "Target lead record not found." });
            if (lead.IsConvertedToDeal) return Conflict(new { message = "Lead already promoted to a deal." });

            // Resolve the buyer contact: prefer the lead's LinkedContactId; otherwise search by email.
            var tenantId = lead.TenantId;
            Contact? buyerContact = null;
            if (!string.IsNullOrEmpty(lead.LinkedContactId))
            {
                buyerContact = await ContactRepository.Query()
                    .FirstOrDefaultAsync(c => c.ContactId == lead.LinkedContactId && c.TenantId == tenantId);
            }

            if (buyerContact == null && !string.IsNullOrEmpty(lead.ContactEmail))
            {
                buyerContact = await ContactRepository.Query()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.EmailAddress == lead.ContactEmail);
            }

            if (buyerContact == null)
            {
                buyerContact = new Contact
                {
                    ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                    TenantId = tenantId,
                    FullName = lead.ClientName,
                    EmailAddress = lead.ContactEmail,
                    PhoneNumber = lead.ContactPhone,
                    ContactType = ContactType.Buyer,
                    PositionJobTitle = "Private Individual",
                    CreatedAt = DateTime.UtcNow
                };
                ContactRepository.Add(buyerContact);
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
                var client = HttpClient.CreateClient();
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
                var matchResponse = await client.PostAsync($"{GatewayUrl}/properties/search?page=1", payload);
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
                var sellerContact = await ContactRepository.Query()
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.FullName.Contains(request.SellerNameIfNewDeal));
                if (sellerContact == null)
                {
                    sellerContact = new Contact
                    {
                        ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                        TenantId = tenantId,
                        FullName = request.SellerNameIfNewDeal,
                        ContactType = ContactType.Seller,
                        PositionJobTitle = "Property Owner / Developer",
                        CreatedAt = DateTime.UtcNow
                    };
                    ContactRepository.Add(sellerContact);
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

            DealRepository.Add(deal);
            lead.IsConvertedToDeal = true;
            await DbContext.SaveChangesAsync();

            return Ok(new
            {
                message = "Lead promoted to deal with property auto-matching.",
                dealId = deal.DealId,
                linkedPropertyId = matchedPropertyId,
                buyerClientId = buyerContact.ContactId,
                sellerClientId = sellerClientId
            });
        }
        [HttpPost("update-lead-stage")]
        public async Task<IActionResult> UpdateLeadStage([FromBody] LeadUpdatePayload payload)
        {
            if (!await PermissionCheck.HasPermissionAsync("leads:edit"))
            {
                Logger.LogWarning("UpdateLeadStage: forbidden — caller lacks leads:edit permission");
                return Forbid();
            }

            var tenantId = GetCurrentTenant();
            var lead = await LeadRepository.Query().FirstOrDefaultAsync(l => l.LeadId == payload.LeadId && l.TenantId == tenantId);
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
                var buyerContact = await ContactRepository.Query()
                    .FirstOrDefaultAsync(c => c.TenantId == lead.TenantId && c.EmailAddress == lead.ContactEmail);

                if (buyerContact == null)
                {
                    buyerContact = new Contact
                    {
                        ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                        TenantId = lead.TenantId,
                        FullName = lead.ClientName,
                        EmailAddress = lead.ContactEmail,
                        PhoneNumber = lead.ContactPhone,
                        ContactType = ContactType.Buyer,
                        PositionJobTitle = "Private Individual",
                        CreatedAt = DateTime.UtcNow
                    };
                    ContactRepository.Add(buyerContact);
                }

                var sellerName = !string.IsNullOrEmpty(payload.SellerNameIfNewDeal) ? payload.SellerNameIfNewDeal : "Unknown Seller Pool";
                var sellerContact = await ContactRepository.Query()
                    .FirstOrDefaultAsync(c => c.TenantId == lead.TenantId && c.FullName.Contains(sellerName));

                if (sellerContact == null)
                {
                    sellerContact = new Contact
                    {
                        ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper(),
                        TenantId = lead.TenantId,
                        FullName = sellerName,
                        ContactType = ContactType.Seller,
                        PositionJobTitle = "Property Owner / Developer",
                        CreatedAt = DateTime.UtcNow
                    };
                    ContactRepository.Add(sellerContact);
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

                DealRepository.Add(dynamicDeal);
                lead.IsConvertedToDeal = true;
            }

            await DbContext.SaveChangesAsync();
            return Ok(new { message = lead.IsConvertedToDeal ? "Lead successfully qualified! Profiles synced and deal initiated." : "Lead stage updated." });
        }
}
