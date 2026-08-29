using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    public class UnitLease
    {
        [Key]
        public int UnitLeaseId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;        // tenant user id (NameIdentifier)

        [Required]
        public string TenantId { get; set; } = string.Empty;       // tenant sandbox

        [Required]
        public int PropertyId { get; set; }

        [Required]
        public int PropertyUnitId { get; set; }

        public DateTime LeaseStart { get; set; }
        public DateTime LeaseEnd { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
