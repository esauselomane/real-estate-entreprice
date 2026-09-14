using System.ComponentModel.DataAnnotations;

namespace Identity.Microservice.Models
{
    public class TenantAgency
    {
        [Key]
        public string TenantId { get; set; } = string.Empty; // e.g., "TEN-001"
        
        [Required]
        public string AgencyName { get; set; } = string.Empty;
        
        public string LogoInitials { get; set; } = string.Empty;
        
        [Required]
        public string ContactEmail { get; set; } = string.Empty;
        
        public string HeadquartersAddress { get; set; } = string.Empty;
        
        public string OperationalStatus { get; set; } = "Active"; // Active, TrialPending, TrialExpired, De-registered
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string BrokerageLicense { get; set; } = string.Empty;

        // 🎯 TRIAL ONBOARDING FIELDS
        public bool IsTrial { get; set; } = false;
        public DateTime? TrialExtensionAppliedAt { get; set; }
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }

        // 🧬 PARENT-AGENCY HIERARCHY (parent tenant this agency rolls up to)
        [MaxLength(64)]
        public string? ParentTenantId { get; set; }

        // 💳 PAYMENT / SUBSCRIPTION FIELDS
        [MaxLength(100)]
        public string? PeachCustomerId { get; set; }

        public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trialing;

        public DateTime? SubscriptionEndsAt { get; set; }

        public int? ActiveSubscriptionId { get; set; }
    }
}
