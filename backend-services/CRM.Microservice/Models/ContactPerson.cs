using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class ContactPerson
    {
        [Key]
        public string ContactPersonId { get; set; } = string.Empty;

        [Required]
        public string AgencyId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Role { get; set; } = "Owner"; // Owner / Tenant / Manager
        public bool IsAgencyAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AgencyId")]
        public AgencyRecord? Agency { get; set; }
    }
}
