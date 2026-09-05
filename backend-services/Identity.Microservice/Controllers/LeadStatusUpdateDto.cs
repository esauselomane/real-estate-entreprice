namespace Identity.Microservice.Controllers
{
    // 🔒 SuperAdmin-only DTO — advances a lead through the conversion funnel.
    public class LeadStatusUpdateDto
    {
        public string Status { get; set; } = string.Empty;   // New | Contacted | QuoteSent | Paid | AgencyCreated
    }
}
