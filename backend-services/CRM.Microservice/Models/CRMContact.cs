using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public class CRMContact
    {
        [Key]
        public string ContactId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string EmailAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        public string PositionJobTitle { get; set; } = "Director";
        public string ContactRoleType { get; set; } = "Buyer";

        public string? AssociatedCompanyId { get; set; }
        public string? AssociatedLegalEntityId { get; set; }

        public string? LinkedDealIds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AssociatedCompanyId")]
        public CRMCompany? CompanyDetails { get; set; }

        [ForeignKey("AssociatedLegalEntityId")]
        public CRMLegalEntity? LegalEntityDetails { get; set; }
    }
}
