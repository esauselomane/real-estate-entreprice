using System.ComponentModel.DataAnnotations;
using CRM.Microservice.Models;

namespace CRM.Microservice.DTOs;

public sealed class UpdateContactRequest
{
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public ContactType ContactType { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        public string? CompanyName { get; set; }

        public string EmailAddress { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? PositionJobTitle { get; set; }
        public string? PrimaryLocation { get; set; }

        public decimal? BudgetLimit { get; set; }

        public string? PropertyTypeInterests { get; set; }
        public string? AreaInterestIds { get; set; }
        public string? Notes { get; set; }

        public string? AssociatedCompanyId { get; set; }
        public string? AssociatedLegalEntityId { get; set; }

        public string? LinkedDealIds { get; set; }

        public string? AgencyId { get; set; }
        public bool IsAgencyAdmin { get; set; } = false;

}
