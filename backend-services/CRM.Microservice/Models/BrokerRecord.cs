using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class BrokerRecord
    {
        [Key]
        public string BrokerId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string AgencyId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = "Broker";
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
