using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class CRMLegalEntity
    {
        [Key]
        public string LegalEntityId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string RegisteredLegalName { get; set; } = string.Empty;

        public string AssociatedCompanyId { get; set; } = string.Empty;

        [Required]
        public string EntityTypeClassifier { get; set; } = "Private Company";

        public string TaxRegistrationIndex { get; set; } = string.Empty;
        public string DeedsOfficeIdentifier { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AssociatedCompanyId")]
        public CRMCompany? CompanyDetails { get; set; }
    }
}
