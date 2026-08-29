using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class CRMCompany
    {
        [Key]
        public string CompanyId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string BusinessName { get; set; } = string.Empty;

        public string RegistrationNumber { get; set; } = string.Empty;
        public string VatNumber { get; set; } = string.Empty;
        public string CorporateWebsite { get; set; } = string.Empty;
        public string HeadOfficeAddress { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CorporateCreditLimit { get; set; }

        public string IndustryVertical { get; set; } = "Real Estate Investment";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
