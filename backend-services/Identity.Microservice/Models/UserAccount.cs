using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    public class UserAccount
    {
        [Key]
        public string UserId { get; set; } = string.Empty; // e.g., "USR-881"

        // 🔒 MANDATORY TENANT DISCRIMINATOR VECTOR COLUMN
        [Required]
        public string TenantId { get; set; } = string.Empty; 

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string SystemRole { get; set; } = "Broker"; // PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant

        public string AvatarUrl { get; set; } = string.Empty; // Round profile picture location path
        
        public string AccountStatus { get; set; } = "Active"; // Active, De-registered
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🎯 TRIAL ONBOARDING FIELDS
        public string ActivationToken { get; set; } = string.Empty;
        public DateTime? ActivationTokenExpiry { get; set; }
        public bool IsTrial { get; set; } = false;
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }

        // Dynamic relational reference linking back to our target onboarding agency profile records
        [ForeignKey("TenantId")]
        public TenantAgency? AgencyDetails { get; set; }
    }
}
