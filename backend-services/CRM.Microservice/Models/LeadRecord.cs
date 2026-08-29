using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class LeadRecord
    {
        [Key]
        public int LeadId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string ClientName { get; set; } = string.Empty;

        public string ContactEmail { get; set; } = string.Empty;
        public string ContactPhone { get; set; } = string.Empty;
        // 🔗 Optional link to an existing CRMContact resolved by name/surname or email on creation.
        public string? LinkedContactId { get; set; } = null;

        [Required]
        public string AllocatedToAgent { get; set; } = string.Empty;

        [Required]
        public LeadWorkflowStage LeadStage { get; set; } = LeadWorkflowStage.Contacted;

        [Required]
        public string PropertyInterestClass { get; set; } = string.Empty;

        [Required]
        public string PreferredArea { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedBudget { get; set; }

        public string LeadNotes { get; set; } = string.Empty;
        public bool IsConvertedToDeal { get; set; } = false;

        // 💰 Commission entitlement captured on the lead and carried into the deal at promotion.
        public string CommissionType { get; set; } = CommissionTypes.Percent;
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionValue { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
