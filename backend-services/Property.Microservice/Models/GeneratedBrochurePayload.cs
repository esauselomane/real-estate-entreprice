using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Property.Microservice.Models
{
    public class GeneratedBrochurePayload
    {
        [Required]
        public int DealId { get; set; } // Tracks target CRM baseline presentation workflow trigger

        [Required]
        public int PropertyId { get; set; } // Binds layout details to master spatial records

        [Required]
        public string BrokerName { get; set; } = string.Empty;
        [Required]
        public string BrokerEmail { get; set; } = string.Empty;
        [Required]
        public string BrokerPhone { get; set; } = string.Empty;
        [Required]
        public string CompanyReferenceNumber { get; set; } = string.Empty;

        public string MarketClass { get; set; } = "Rental";
        public decimal TenantInstallationAllowance { get; set; }
        public string CompleteAddress { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }

        // Broker-selected marketing image refs (limit enforced by the frontend, capped at 5).
        public List<string> SelectedImages { get; set; } = new();
        // Broker-curated sub-unit line items (max 5) the worker renders as disclosure sheets.
        public List<BrochureSubUnit> SubUnits { get; set; } = new();

        // Sale/Rental conditional matrix (Rental uses the first group, Sale uses SalePrice).
        public decimal BaseRentalRate { get; set; }
        public decimal RentEscalationPercentage { get; set; }
        public int LeaseTermMonths { get; set; } = 12;
        public decimal SalePrice { get; set; }

        // CRM brochure card orchestration handle so the worker can callback and close the deal loop.
        public int BrochureCardId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string ClientEmail { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;

        public DateTime LayoutGeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
