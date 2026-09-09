using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRM.Microservice.Models
{
    public enum ContactType
    {
        Buyer,
        Seller,
        Broker,
        Owner,
        Tenant,
        Staff,
        Manager
    }

    public class Contact
    {
        [Key]
        public string ContactId { get; set; } = string.Empty;

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public ContactType ContactType { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public string EmailAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? PositionJobTitle { get; set; }
        public string? PrimaryLocation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? BudgetLimit { get; set; }

        public string? PropertyTypeInterests { get; set; }
        public string? AreaInterestIds { get; set; }
        public string? Notes { get; set; }

        public string? AssociatedCompanyId { get; set; }
        public string? AssociatedLegalEntityId { get; set; }

        public string? LinkedDealIds { get; set; }

        public string? AgencyId { get; set; }
        public bool IsAgencyAdmin { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("AssociatedCompanyId")]
        public CRMCompany? CompanyDetails { get; set; }

        [ForeignKey("AssociatedLegalEntityId")]
        public CRMLegalEntity? LegalEntityDetails { get; set; }

        [ForeignKey("AgencyId")]
        public AgencyRecord? Agency { get; set; }
    }
}
