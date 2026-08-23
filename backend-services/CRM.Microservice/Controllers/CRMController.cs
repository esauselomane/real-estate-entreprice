using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class CRMController : ControllerBase
    {
        private readonly CRMDbContext _context;

        public CRMController(CRMDbContext context)
        {
            _context = context;
        }

        public class LeadUpdatePayload { public int LeadId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; public int? LinkToPropertyId { get; set; } public string SellerNameIfNewDeal { get; set; } = string.Empty; }
        public class DealUpdatePayload { public int DealId { get; set; } public string NextStage { get; set; } = string.Empty; public string NotesUpdate { get; set; } = string.Empty; }
        public class ReallocatePayload { public int TargetId { get; set; } public bool IsDeal { get; set; } public string TargetAgentName { get; set; } = string.Empty; }

        [HttpGet("companies")]
        public async Task<IActionResult> GetCompanies([FromQuery] string tenantId)
        {
            var companies = await _context.CRMCompanies.Where(c => c.TenantId == tenantId).ToListAsync();
            return Ok(companies);
        }

        [HttpGet("owners-tenants")]
        public async Task<IActionResult> GetOwnerTenantRecords([FromQuery] string tenantId)
        {
            var rows = await _context.OwnerTenantRecords
                .Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return Ok(rows);
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
            existing.Notes = updatedRecord.Notes;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpGet("agencies")]
        public async Task<IActionResult> GetAgencies([FromQuery] string tenantId)
        {
            var agencies = await _context.AgencyRecords
                .Where(a => string.IsNullOrWhiteSpace(tenantId) || a.TenantId == tenantId)
                .ToListAsync();
            return Ok(agencies);
        }

        [HttpPost("agencies")]
        public async Task<IActionResult> CreateAgency([FromBody] AgencyRecord agency)
        {
            if (agency == null || string.IsNullOrWhiteSpace(agency.Name))
                return BadRequest(new { message = "Agency name is required." });

            if (string.IsNullOrWhiteSpace(agency.AgencyId))
                agency.AgencyId = "TEN-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();

            agency.CreatedAt = DateTime.UtcNow;
            _context.AgencyRecords.Add(agency);
            await _context.SaveChangesAsync();
            return Ok(agency);
        }

        [HttpGet("brokers")]
        public async Task<IActionResult> GetBrokers([FromQuery] string tenantId)
        {
            var brokers = await _context.BrokerRecords
                .Where(b => string.IsNullOrWhiteSpace(tenantId) || b.TenantId == tenantId)
                .ToListAsync();
            return Ok(brokers);
        }

        [HttpPost("brokers")]
        public async Task<IActionResult> CreateBroker([FromBody] BrokerRecord broker)
        {
            if (broker == null || string.IsNullOrWhiteSpace(broker.FullName))
                return BadRequest(new { message = "Broker details are required." });

            if (string.IsNullOrWhiteSpace(broker.BrokerId))
                broker.BrokerId = "BRK-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();

            broker.CreatedAt = DateTime.UtcNow;
            _context.BrokerRecords.Add(broker);
            await _context.SaveChangesAsync();
            return Ok(broker);
        }

        [HttpPost("company/create")]
        public async Task<IActionResult> CreateCompany([FromBody] CRMCompany company)
        {
            if (string.IsNullOrEmpty(company.CompanyId)) company.CompanyId = "COM-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();
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
        public async Task<IActionResult> GetLegalEntities([FromQuery] string tenantId)
        {
            var entities = await _context.CRMLegalEntities
                .Include(e => e.CompanyDetails)
                .Where(e => e.TenantId == tenantId)
                .ToListAsync();
            return Ok(entities);
        }

        [HttpPost("legal-entity/create")]
        public async Task<IActionResult> CreateLegalEntity([FromBody] CRMLegalEntity legalEntity)
        {
            if (string.IsNullOrEmpty(legalEntity.LegalEntityId))
                legalEntity.LegalEntityId = "LGE-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();

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
        public async Task<IActionResult> GetContacts([FromQuery] string tenantId)
        {
            var contacts = await _context.CRMContacts
                .Include(c => c.CompanyDetails)
                .Include(c => c.LegalEntityDetails)
                .Where(c => c.TenantId == tenantId)
                .ToListAsync();
            return Ok(contacts);
        }

        [HttpPost("contact/create")]
        public async Task<IActionResult> CreateContact([FromBody] CRMContact contact)
        {
            if (string.IsNullOrEmpty(contact.ContactId))
                contact.ContactId = "CON-" + Guid.NewGuid().ToString().Substring(0, 5).ToUpper();

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
        public async Task<IActionResult> GetCRMBoards([FromQuery] string tenantId, [FromQuery] string agentName, [FromQuery] string userRole)
        {
            var leadsQuery = _context.LeadRecords.Where(l => l.TenantId == tenantId && !l.IsConvertedToDeal);
            var dealsQuery = _context.DealRecords.Where(d => d.TenantId == tenantId);

            if (userRole == "Agent")
            {
                leadsQuery = leadsQuery.Where(l => l.AllocatedToAgent == agentName);
                dealsQuery = dealsQuery.Where(d => d.AllocatedToAgent == agentName);
            }

            var activeLeads = await leadsQuery.ToListAsync();
            var activeDeals = await dealsQuery.ToListAsync();

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

            newLead.LeadStage = LeadWorkflowStage.Contacted;
            newLead.IsConvertedToDeal = false;
            newLead.CreatedAt = DateTime.UtcNow;

            _context.LeadRecords.Add(newLead);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Unverified lead record successfully catalogued.", leadId = newLead.LeadId });
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
            var lead = await _context.LeadRecords.FindAsync(payload.LeadId);
            if (lead == null) return NotFound(new { message = "Target lead tracking record not found." });

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
                    CreatedAt = DateTime.UtcNow
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
            var deal = await _context.DealRecords.FindAsync(payload.DealId);
            if (deal == null) return NotFound(new { message = "Target deal record not found." });

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

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Deal successfully progressed to {payload.NextStage}." });
        }

        [HttpGet("report/summary")]
        public async Task<IActionResult> GetPipelineReportSummary([FromQuery] string tenantId)
        {
            var tenantLeads = await _context.LeadRecords.Where(l => l.TenantId == tenantId && !l.IsConvertedToDeal).ToListAsync();
            var tenantDeals = await _context.DealRecords.Where(d => d.TenantId == tenantId).ToListAsync();

            var summaryReport = new PipelineSummaryReport
            {
                ActiveLeadsCount = tenantLeads.Count,
                ValuationsInProcessCount = tenantDeals.Count(d => d.DealStage != DealWorkflowStage.ClosedWon && d.DealStage != DealWorkflowStage.ClosedLost),
                ConveyancingAtDeedsCount = tenantDeals.Count(d => d.DealStage == DealWorkflowStage.ProposalQuoteSent || d.DealStage == DealWorkflowStage.Negotiation),
                FinalizedClosedCount = tenantDeals.Count(d => d.DealStage == DealWorkflowStage.ClosedWon),
                TotalPipelineEstimatedValue = tenantDeals.Where(d => d.DealStage != DealWorkflowStage.ClosedWon && d.DealStage != DealWorkflowStage.ClosedLost).Sum(d => d.TransactionValue)
            };

            return Ok(summaryReport);
        }

        [HttpGet("report/overview")]
        public async Task<IActionResult> GetReportOverview([FromQuery] string tenantId, [FromQuery] int? year)
        {
            var activeYear = year ?? DateTime.UtcNow.Year;
            var tenantLeads = await _context.LeadRecords
                .Where(l => l.TenantId == tenantId && l.CreatedAt.Year == activeYear)
                .ToListAsync();
            var tenantDeals = await _context.DealRecords
                .Where(d => d.TenantId == tenantId && d.CreatedAt.Year == activeYear)
                .ToListAsync();

            var monthlyBreakdown = Enumerable.Range(1, 12)
                .Select(month => new ReportDrilldownRow
                {
                    Period = new DateTime(activeYear, month, 1).ToString("MMM"),
                    Leads = tenantLeads.Count(l => l.CreatedAt.Month == month),
                    Deals = tenantDeals.Count(d => d.CreatedAt.Month == month),
                    Revenue = tenantDeals.Where(d => d.CreatedAt.Month == month).Sum(d => d.TransactionValue)
                })
                .ToList();

            return Ok(new CRMReportOverview
            {
                TotalLeads = tenantLeads.Count,
                TotalDeals = tenantDeals.Count,
                TotalRevenue = tenantDeals.Sum(d => d.TransactionValue),
                MonthlyBreakdown = monthlyBreakdown
            });
        }
    }
}
