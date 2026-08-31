using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Developer.API.Models
{
    public class AgencyApiKey
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required] [MaxLength(100)] public string AgencyId { get; set; } = string.Empty;
        [Required] [MaxLength(200)] public string AgencyName { get; set; } = string.Empty;
        [Required] [MaxLength(450)] public string KeyHash { get; set; } = string.Empty;
        [Required] [MaxLength(100)] public string KeyPrefix { get; set; } = string.Empty;
        [Required] [MaxLength(450)] public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AllowedIps { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsRevoked { get; set; } = false;
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
