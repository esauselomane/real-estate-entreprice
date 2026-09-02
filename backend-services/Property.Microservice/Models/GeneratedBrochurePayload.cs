using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    public class GeneratedBrochurePayload
    {
        [Required]
        public int DealId { get; set; }

        [Required]
        public string BrokerName { get; set; } = string.Empty;
        [Required]
        public string BrokerEmail { get; set; } = string.Empty;
        [Required]
        public string BrokerPhone { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;

        public string MarketClass { get; set; } = "Rental";
        public decimal TenantInstallationAllowance { get; set; }

        public List<BrochureSubUnit> SubUnits { get; set; } = new();

        public int BrochureCardId { get; set; }

        public DateTime LayoutGeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
