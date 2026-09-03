using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class OwnerTenantRecord
    {
        [Key]
        public int OwnerTenantId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string RecordType { get; set; } = "Owner";

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PrimaryLocation { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetLimit { get; set; }

        // 📋 Interests: JSON-encoded lists so owners/tenants can express demand
        // across multiple property types and geographic areas (autofilled from Geography).
        public string PropertyTypeInterests { get; set; } = string.Empty;
        public string AreaInterestIds { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
