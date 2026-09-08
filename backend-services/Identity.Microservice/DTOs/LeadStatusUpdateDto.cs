using Identity.Microservice.Models;

namespace Identity.Microservice.DTOs
{
    // 🔒 SuperAdmin-only DTO — advances a lead through the conversion funnel.
    public record class LeadStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;   // New | Contacted | QuoteSent | Paid | AgencyCreated
    }
}
