namespace Identity.Microservice.Controllers
{
    // 🔒 SuperAdmin-only DTO — records an action on a lead's onboarding workflow.
    public class LeadWorkflowActionDto
    {
        public string ActionType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public decimal? QuoteAmount { get; set; }
        public string? Currency { get; set; } = "ZAR";
        public string? PaymentReference { get; set; }
        public string? AgencyId { get; set; }
    }
}
