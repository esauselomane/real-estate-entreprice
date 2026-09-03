using System.ComponentModel.DataAnnotations;

namespace Property.Microservice.Models
{
    public class MaintenanceCall
    {
        [Key]
        public int MaintenanceCallId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string RequestedByUserId { get; set; } = string.Empty;

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int PropertyUnitId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Open"; // Open, In Progress, Resolved, Closed
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
