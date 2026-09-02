using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class BrochureCard
    {
        [Key]
        public int CardId { get; set; }

        [Required] public string TenantId { get; set; } = string.Empty;
        [Required] public int DealId { get; set; }
        [Required] public string BrokerAgentId { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;

        public BrochureCardStatus Status { get; set; } = BrochureCardStatus.Active;
        public bool IsDeleted { get; set; } = false;
        public string GeneratedFilePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<BrochureCardLine> Lines { get; set; } = new List<BrochureCardLine>();
    }
}
