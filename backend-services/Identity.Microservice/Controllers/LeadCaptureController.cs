using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Identity.Microservice.Data;
using Identity.Microservice.Models;
using System.Linq;
using System.Security.Claims;

namespace Identity.Microservice.Controllers
{
    // 📋 Public lead/intake capture for the website (Bibsy chatbot + pricing page).
    // Route sits under the existing /api/system/* umbrella so it is reached through
    // the gateway at /gateway/system/lead-requests — no new gateway route required.
    // POST is AllowAnonymous (the website is unauthenticated); GET/PUT are SuperAdmin-only.
    [ApiController]
    [Route("api/system/lead-requests")]
    public class LeadCaptureController : ControllerBase
    {
        private readonly IdentityDbContext _context;

        public LeadCaptureController(IdentityDbContext context)
        {
            _context = context;
        }

        // 🟢 PUBLIC — capture a new lead from the marketing website.
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> CreateLead([FromBody] LeadCaptureRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { message = "Full name and email are required." });
            if (!dto.Email.Contains("@") || !dto.Email.Contains(".", StringComparison.Ordinal))
                return BadRequest(new { message = "A valid email is required." });

            Enum.TryParse<LeadUserType>(dto.UserType, true, out var userType);
            LeadPackage? pkg = null;
            if (!string.IsNullOrWhiteSpace(dto.Package) &&
                Enum.TryParse<LeadPackage>(dto.Package, true, out var parsedPkg))
            {
                pkg = parsedPkg;
            }

            var lead = new LeadCaptureRequest
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                Company = dto.Company,
                UserType = userType,
                Package = pkg,
                DemoRequested = dto.DemoRequested,
                Message = dto.Message,
                Source = dto.Source ?? "website",
                Status = LeadStatus.New
            };

            _context.LeadCaptureRequests.Add(lead);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Your details have been captured. A broker will contact you shortly.",
                leadId = lead.Id
            });
        }

        // 🔐 SuperAdmin — list leads, optionally filtered by status.
        [HttpGet]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetLeads([FromQuery] string? status = null)
        {
            IQueryable<LeadCaptureRequest> query = _context.LeadCaptureRequests.AsNoTracking().OrderByDescending(l => l.CreatedAt);
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
                query = query.Where(l => l.Status == Enum.Parse<LeadStatus>(status, true));

            var list = await query
                .Select(l => new
                {
                    l.Id,
                    l.FullName,
                    l.Email,
                    l.Phone,
                    l.Company,
                    userType = l.UserType.ToString(),
                    package = l.Package.HasValue ? l.Package.Value.ToString() : null,
                    l.DemoRequested,
                    l.Message,
                    l.Source,
                    status = l.Status.ToString(),
                    l.HandledBy,
                    l.CreatedAt,
                    l.UpdatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        // 🔐 SuperAdmin — advance a lead through the funnel.
        [HttpPut("{id:int}/status")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] LeadStatusUpdateDto dto)
        {
            var lead = await _context.LeadCaptureRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (lead == null) return NotFound(new { message = "Lead not found." });

            if (string.IsNullOrWhiteSpace(dto?.Status) ||
                !Enum.TryParse<LeadStatus>(dto.Status, true, out var newStatus))
                return BadRequest(new { message = "Valid status is required (New, Contacted, QuoteSent, Paid, AgencyCreated)." });

            lead.Status = newStatus;
            lead.HandledBy = User?.FindFirst(ClaimTypes.Name)?.Value ?? "SuperAdmin";
            lead.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Lead status updated.",
                leadId = lead.Id,
                status = lead.Status.ToString()
            });
        }

        // 🟢 PUBLIC — list active pricing packages for the marketing website.
        [HttpGet("pricing-packages")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPricingPackages()
        {
            var packages = await _context.PricingPackages
                .AsNoTracking()
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .Select(p => new
                {
                    p.Code,
                    p.Name,
                    p.Description,
                    monthlyPrice = p.MonthlyPrice,
                    annualPrice = p.AnnualPrice,
                    p.FeaturesJson
                })
                .ToListAsync();

            var result = packages.Select(p => new
            {
                p.Code,
                p.Name,
                p.Description,
                p.monthlyPrice,
                p.annualPrice,
                features = System.Text.Json.JsonSerializer.Deserialize<string[]>(p.FeaturesJson!) ?? Array.Empty<string>()
            });

            return Ok(result);
        }

        // 🔐 SuperAdmin — get workflow history for a lead.
        [HttpGet("{id:int}/workflow")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> GetWorkflow(int id)
        {
            var history = await _context.LeadWorkflows
                .AsNoTracking()
                .Where(w => w.LeadId == id)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    w.Id,
                    actionType = w.ActionType.ToString(),
                    w.Notes,
                    w.PerformedBy,
                    w.QuoteAmount,
                    w.Currency,
                    w.PaymentReference,
                    w.AgencyId,
                    w.CreatedAt
                })
                .ToListAsync();

            return Ok(history);
        }

        // 🔐 SuperAdmin — add a workflow action to a lead.
        [HttpPost("{id:int}/workflow")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> AddWorkflowAction(int id, [FromBody] LeadWorkflowActionDto dto)
        {
            var lead = await _context.LeadCaptureRequests.FirstOrDefaultAsync(l => l.Id == id);
            if (lead == null) return NotFound(new { message = "Lead not found." });

            if (string.IsNullOrWhiteSpace(dto?.ActionType) ||
                !Enum.TryParse<LeadWorkflowActionType>(dto.ActionType, true, out var actionType))
                return BadRequest(new { message = "Valid action type is required (CallbackScheduled, QuoteSent, PaymentReceived, AgencyProvisioned)." });

            var workflow = new LeadWorkflow
            {
                LeadId = id,
                ActionType = actionType,
                Notes = dto.Notes,
                PerformedBy = User?.FindFirst(ClaimTypes.Name)?.Value ?? "SuperAdmin",
                QuoteAmount = dto.QuoteAmount,
                Currency = dto.Currency ?? "ZAR",
                PaymentReference = dto.PaymentReference,
                AgencyId = dto.AgencyId
            };

            _context.LeadWorkflows.Add(workflow);

            if (actionType == LeadWorkflowActionType.CallbackScheduled)
            {
                lead.Status = LeadStatus.Contacted;
                lead.HandledBy = workflow.PerformedBy;
            }
            else if (actionType == LeadWorkflowActionType.QuoteSent)
            {
                lead.Status = LeadStatus.QuoteSent;
                lead.HandledBy = workflow.PerformedBy;
            }
            else if (actionType == LeadWorkflowActionType.PaymentReceived)
            {
                lead.Status = LeadStatus.Paid;
                lead.HandledBy = workflow.PerformedBy;
            }
            else if (actionType == LeadWorkflowActionType.AgencyProvisioned)
            {
                lead.Status = LeadStatus.AgencyCreated;
                lead.HandledBy = workflow.PerformedBy;
            }

            lead.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Workflow action recorded.",
                leadId = lead.Id,
                action = actionType.ToString(),
                status = lead.Status.ToString()
            });
        }
    }
}
