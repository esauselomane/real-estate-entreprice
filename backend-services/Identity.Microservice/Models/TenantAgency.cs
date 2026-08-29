using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        
        public string OperationalStatus { get; set; } = "Active"; // Active, De-registered
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
