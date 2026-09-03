using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    public enum LeadWorkflowActionType
    {
        CallbackScheduled = 1,
        QuoteSent = 2,
        PaymentReceived = 3,
        AgencyProvisioned = 4
    }

    public class LeadWorkflow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] public int LeadId { get; set; }
        public LeadCaptureRequest? Lead { get; set; }

        [Required] public LeadWorkflowActionType ActionType { get; set; }
        public string? Notes { get; set; }
        public string? PerformedBy { get; set; }
        public decimal? QuoteAmount { get; set; }
        public string? Currency { get; set; } = "ZAR";
        public string? PaymentReference { get; set; }
        public string? AgencyId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
