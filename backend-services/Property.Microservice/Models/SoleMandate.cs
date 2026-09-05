using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    // 📜 Sole-mandate history for a property. A property can have many
    // SoleMandates over its lifetime, but only ONE can be active at a
    // time (enforced via a filtered unique index on IsActive=1 in
    // Program.cs). When a new active mandate is created the server
    // auto-closes any previous active one in the same request, so the
    // "active" row is always the most recent open agreement.
    public class SoleMandate
    {
        [Key]
        public int SoleMandateId { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        // True while the mandate is in force. Server flips this to false
        // (ClosedAt) when a new active mandate is created for the same
        // property, or when the agent calls DELETE /mandates/{id} to
        // terminate early.
        public bool IsActive { get; set; } = true;

        public DateTime? ClosedAt { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PropertyId")]
        public PropertyAsset? Property { get; set; }
    }
}
