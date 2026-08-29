using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
    // 🏢 The Central Multi-Tenant Enterprise Agency Definition Model
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
        
        public string OperationalStatus { get; set; } = "Active"; // Active, De-registered
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // 👤 Tenant-Isolated Security Profiling Entity Model
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
        // Dynamic relational reference linking back to our target onboarding agency profile records
        [ForeignKey("TenantId")]
        public TenantAgency? AgencyDetails { get; set; }
    }

    // 🔒 GRANULAR SECURABLE ACCESS CONTROL SYSTEM CLAIMS MODEL
    public class UserAccessClaim
    {
        [Key]
        public int ClaimId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Granular platform permissions matching your configuration panel
        public bool CanDeleteProperties { get; set; } = false;
        public bool CanExportFinancialReports { get; set; } = true;
        public bool CanBypassValuationStages { get; set; } = false;

        [ForeignKey("UserId")]
        public UserAccount? AccountDetails { get; set; }
    }

    // 🔐 CONFIGURABLE SYSTEM ROLE DEFINITION — what an administrator can grant / revoke
    // from the System Admin "Roles" configuration tab. The hard-coded role names used by
    // AuthController.RegisterUser ("PlatformAdmin","AgencyAdmin","Broker","Owner","Tenant")
    // are now driven from this table so the platform contract stays data-driven.
    public class SystemRoleDefinition
    {
        [Key]
        public int RoleDefId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty; // PlatformAdmin, AgencyAdmin, Broker, Owner, Tenant

        public string Description { get; set; } = string.Empty;

        // Scope of the bearer who may provision into this role (mirrors AuthController matrix)
        public bool IsPlatformScoped { get; set; } = false;   // PlatformAdmin may assign
        public bool IsAgencyScoped { get; set; } = false;     // AgencyAdmin may assign

        // JSON array of permission keys granted to holders of this role,
        // e.g. ["CanDeleteProperties","CanExportFinancialReports","CanBypassValuationStages"]
        public string PermissionsJson { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
