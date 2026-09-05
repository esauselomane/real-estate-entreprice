using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class OperationalRecord
    {
        [Key]
        public int OperationalRecordId { get; set; }
        [Required] public string TenantId { get; set; } = string.Empty;
        [Required] public string Module { get; set; } = string.Empty;
        [Required] public string Title { get; set; } = string.Empty;
        public string PropertyReference { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string Status { get; set; } = "Open";
        public DateTime? DueDate { get; set; }
        [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
        public string Details { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Unit-level linkage: binds this operational record to a specific property
        // and its sub-unit so it can be surfaced on the property page without the
        // user re-entering property / unit / address. Cross-service reference by ID
        // (mirrors DealRecord.LinkedPropertyId) since Property & CRM use separate DBs.
        public int? PropertyId { get; set; }
        public int? PropertyUnitId { get; set; }
    }
}
