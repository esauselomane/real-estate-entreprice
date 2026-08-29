using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Identity.Microservice.Models
{
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
